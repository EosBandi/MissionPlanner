using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Utilities;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Params
{
    public sealed record ParamValue(string Name, double Value, string Type, double? Default);

    public sealed record ParamOption(long Value, string Name);

    /// <summary>
    /// ArduPilot parameter documentation (apm.pdef.xml), as shown in MP's Full Parameter List.
    /// </summary>
    public sealed record ParamMetadata(
        string Name,
        string DisplayName,
        string Description,
        string Units,
        double? Min,
        double? Max,
        double? Increment,
        IReadOnlyList<ParamOption> Values,
        IReadOnlyList<ParamOption> Bitmask,
        string User,
        bool RebootRequired,
        bool ReadOnly);

    public sealed record ParamDiff(string Name, double? VehicleValue, double FileValue);

    public sealed record ParamSetResult(string Name, bool Accepted, double? Value, string Error, bool RebootRequired);

    public sealed record ParamEvent(string Event, uint SysId, byte CompId, int Count, IReadOnlyList<ParamValue> Changed);

    /// <summary>
    /// Parameters for a vehicle: download (cache → MAVFTP → classic), get/set, metadata, .param files and compare.
    /// Port of the param handling spread over MainV2.doConnect, ConfigRawParams and paramcompare.
    /// </summary>
    public sealed class ParamService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ParamService));

        private readonly LinkManager links;
        private readonly VehicleService vehicles;
        private readonly JobManager jobs;
        private readonly EventHub events;
        private readonly BackendOptions options;
        private readonly ConcurrentDictionary<string, ParamMetadata> metadataCache = new();

        public ParamService(LinkManager links, VehicleService vehicles, JobManager jobs, EventHub events,
            BackendOptions options)
        {
            this.links = links;
            this.vehicles = vehicles;
            this.jobs = jobs;
            this.events = events;
            this.options = options;
        }

        /// <summary>
        /// Download the full parameter list as a job, then refresh the metadata for the firmware version.
        /// </summary>
        public Job StartDownload(uint sysid, byte compid, bool allowCache = true)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            return jobs.Start("params.download", $"Download parameters {sysid}/{compid}", async ctx =>
            {
                string source;
                if (allowCache && TryLoadCache(mav))
                {
                    source = "cache";
                }
                else
                {
                    using var progress = new Timer(_ =>
                    {
                        var p = mav.param;
                        if (p.TotalReported > 0)
                            ctx.Report((int)(p.TotalReceived * 100.0 / p.TotalReported),
                                $"{p.TotalReceived}/{p.TotalReported}");
                    }, null, 200, 200);

                    ctx.Report(-1, "Requesting parameters");
                    // tries MAVFTP @PARAM/param.pck first (fast, includes defaults), falls back to PARAM_REQUEST_LIST
                    var useFtp = Settings.Instance.GetBoolean("UseMavFtpParams", true);
                    await link.RunExclusiveAsync(m => useFtp
                            ? m.getParamListMavftpAsync(sysid, compid)
                            : m.getParamListAsync(sysid, compid), ctx.Token)
                        .ConfigureAwait(false);
                    source = useFtp && mav.param.Any(p => p.default_value.HasValue) ? "mavftp" : "mavlink";
                }

                var count = mav.param.Count;
                if (count == 0 || count < mav.param.TotalReported)
                    throw new IOException($"incomplete parameter list {count}/{mav.param.TotalReported}");

                ctx.Report(100, $"{count} parameters ({source})");
                events.Publish(Topics.Params, new ParamEvent("loaded", sysid, compid, count, null));
                _ = StartMetadataUpdate(mav.cs.version);
                return new { count, source };
            }, link.Id);
        }

        public IReadOnlyList<ParamValue> List(uint sysid, byte compid)
        {
            var mav = vehicles.Resolve(sysid, compid).mav;
            lock (mav.param)
                return mav.param.OrderBy(p => p.Name, StringComparer.Ordinal).Select(ToValue).ToList();
        }

        public ParamValue Get(uint sysid, byte compid, string name)
        {
            var mav = vehicles.Resolve(sysid, compid).mav;
            return mav.param.ContainsKey(name) ? ToValue(mav.param[name]) : null;
        }

        /// <summary>
        /// Write one parameter and confirm the value the vehicle echoes back.
        /// </summary>
        public async Task<ParamSetResult> SetAsync(uint sysid, byte compid, string name, double value,
            bool force = false, CancellationToken ct = default)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            var result = await SetOneAsync(link, mav, name, value, force, ct).ConfigureAwait(false);
            if (result.Accepted)
                events.Publish(Topics.Params,
                    new ParamEvent("changed", sysid, compid, mav.param.Count, new[] { Get(sysid, compid, name) }));
            return result;
        }

        /// <summary>
        /// Write many parameters as a job (e.g. after a file compare). Unknown names are reported, not fatal.
        /// </summary>
        public Job StartBulkSet(uint sysid, byte compid, IReadOnlyDictionary<string, double> values)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            return jobs.Start("params.write", $"Write {values.Count} parameters {sysid}/{compid}", async ctx =>
            {
                var results = new List<ParamSetResult>();
                var done = 0;
                foreach (var (name, value) in values)
                {
                    ctx.Token.ThrowIfCancellationRequested();
                    results.Add(await SetOneAsync(link, mav, name, value, false, ctx.Token).ConfigureAwait(false));
                    ctx.Report(++done * 100 / Math.Max(1, values.Count), name);
                }

                ctx.Report(100, $"{results.Count(r => r.Accepted)}/{results.Count} written");
                var changed = results.Where(r => r.Accepted).Select(r => Get(sysid, compid, r.Name)).ToList();
                events.Publish(Topics.Params, new ParamEvent("changed", sysid, compid, mav.param.Count, changed));
                return new
                {
                    written = results.Count(r => r.Accepted),
                    failed = results.Where(r => !r.Accepted).ToList(),
                    rebootRequired = results.Where(r => r.Accepted && r.RebootRequired).Select(r => r.Name).ToList()
                };
            }, link.Id);
        }

        /// <summary>
        /// Values in <paramref name="file"/> that differ from (or are missing on) the vehicle, as MP's compare dialog.
        /// </summary>
        public IReadOnlyList<ParamDiff> Compare(uint sysid, byte compid, IReadOnlyDictionary<string, double> file)
        {
            var mav = vehicles.Resolve(sysid, compid).mav;
            var diffs = new List<ParamDiff>();
            foreach (var (name, fileValue) in file.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!mav.param.ContainsKey(name))
                {
                    diffs.Add(new ParamDiff(name, null, fileValue));
                    continue;
                }

                var current = mav.param[name].Value;
                // values travel as float on the wire; compare at that precision
                if ((float)current != (float)fileValue)
                    diffs.Add(new ParamDiff(name, current, fileValue));
            }

            return diffs;
        }

        /// <summary>
        /// The vehicle's parameters in Mission Planner's .param format.
        /// </summary>
        public string ExportParamFile(uint sysid, byte compid)
        {
            var mav = vehicles.Resolve(sysid, compid).mav;
            var table = new Hashtable();
            lock (mav.param)
                foreach (var p in mav.param)
                    table[p.Name] = p.Value;

            var temp = Path.GetTempFileName();
            try
            {
                ParamFile.SaveParamFile(temp, table);
                return File.ReadAllText(temp);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        /// <summary>
        /// Parse .param/.parm text: MP ("NAME,value") and MAVProxy ("NAME value") via MP's ParamFile, plus QGC
        /// ("sysid compid NAME value type") which is rewritten to MP format first.
        /// </summary>
        public static Dictionary<string, double> ParseParamFile(string text)
        {
            var lines = (text ?? "").Split('\n').Select(line =>
            {
                var cols = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                return cols.Length >= 4 && !line.TrimStart().StartsWith("#") &&
                       int.TryParse(cols[0], out _) && int.TryParse(cols[1], out _)
                    ? cols[2] + "," + cols[3]
                    : line.TrimEnd('\r');
            });

            var temp = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(temp, lines);
                return ParamFile.loadParamFile(temp);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        /// <summary>
        /// Metadata for the given parameter names (all of the vehicle's parameters when null).
        /// </summary>
        public IReadOnlyDictionary<string, ParamMetadata> GetMetadata(uint sysid, byte compid,
            IEnumerable<string> names = null)
        {
            var mav = vehicles.Resolve(sysid, compid).mav;
            var firmware = mav.cs.firmware.ToString();
            var selected = names?.ToList() ?? mav.param.Select(p => p.Name).ToList();
            var result = new Dictionary<string, ParamMetadata>(StringComparer.Ordinal);
            foreach (var name in selected)
            {
                var meta = metadataCache.GetOrAdd(firmware + "|" + name, _ => LoadMetadata(name, firmware));
                if (meta != null)
                    result[name] = meta;
            }

            return result;
        }

        /// <summary>
        /// Fetch apm.pdef.xml for a firmware version (or the latest when null) from autotest.ardupilot.org.
        /// Files are cached for 7 days in the data directory.
        /// </summary>
        public Job StartMetadataUpdate(Version version = null, bool force = false)
        {
            return jobs.Start("params.metadata", "Update parameter metadata", async ctx =>
            {
                ctx.Report(-1, "Downloading apm.pdef.xml");
                if (version != null && version.Major > 0)
                    await ParameterMetaDataRepositoryAPMpdef.GetMetaDataVersioned(
                        new Version(version.Major, version.Minor, Math.Max(0, version.Build))).ConfigureAwait(false);
                else
                    await ParameterMetaDataRepositoryAPMpdef.GetMetaData(force).ConfigureAwait(false);
                metadataCache.Clear();
                return new { version = version?.ToString() };
            });
        }

        private bool TryLoadCache(MAVState mav)
        {
            try
            {
                var maxAge = options.ParamCacheMaxAge;
                var path = mav.ParamCachePath;
                if (maxAge <= TimeSpan.Zero || string.IsNullOrEmpty(mav.cs.uid2) || !File.Exists(path) ||
                    File.GetLastWriteTime(path) < DateTime.Now - maxAge)
                    return false;

                var list = File.ReadAllText(path).FromJSON<MAVLink.MAVLinkParamList>();
                if (list == null || list.Count == 0)
                    return false;

                lock (mav.param)
                {
                    mav.param.Clear();
                    mav.param.AddRange(list);
                    mav.param.TotalReported = list.Count;
                }

                foreach (var p in list)
                    mav.param_types[p.Name] = p.Type;
                log.Info($"loaded {list.Count} params from cache {path}");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn("param cache load failed", ex);
                return false;
            }
        }

        private async Task<ParamSetResult> SetOneAsync(Link link, MAVState mav, string name, double value, bool force,
            CancellationToken ct)
        {
            var reboot = ParameterMetaDataRepository.GetParameterRebootRequired(name, mav.cs.firmware.ToString());
            if (!mav.param.ContainsKey(name))
                return new ParamSetResult(name, false, null, "unknown parameter", reboot);

            // a write that does not change the value never needs a reboot
            reboot &= (float)mav.param[name].Value != (float)value;
            try
            {
                var ok = await link.RunExclusiveAsync(m => m.setParamAsync(mav.sysid, mav.compid, name, value, force),
                    ct).ConfigureAwait(false);
                return new ParamSetResult(name, ok, mav.param[name].Value, ok ? null : "no confirmation from vehicle",
                    reboot);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ParamSetResult(name, false, mav.param[name].Value, ex.Message, reboot);
            }
        }

        private static ParamValue ToValue(MAVLink.MAVLinkParam p) =>
            new ParamValue(p.Name, p.Value, p.Type.ToString(), p.default_value);

        private static ParamMetadata LoadMetadata(string name, string firmware)
        {
            string Meta(string key) => ParameterMetaDataRepository.GetParameterMetaData(name, key, firmware);

            var displayName = Meta(ParameterMetaDataConstants.DisplayName);
            var description = Meta(ParameterMetaDataConstants.Description);
            if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(description))
                return null;

            double min = 0, max = 0, inc = 0;
            var hasRange = ParameterMetaDataRepository.GetParameterRange(name, ref min, ref max, firmware);
            var hasInc = ParameterMetaDataRepository.GetParameterIncrement(name, ref inc, firmware);

            return new ParamMetadata(
                name,
                displayName,
                description,
                Meta(ParameterMetaDataConstants.Units),
                hasRange ? min : null,
                hasRange ? max : null,
                hasInc ? inc : null,
                ParameterMetaDataRepository.GetParameterOptionsInt(name, firmware)
                    .Select(kv => new ParamOption(kv.Key, kv.Value)).ToList(),
                ParameterMetaDataRepository.GetParameterBitMaskInt(name, firmware)
                    .Select(kv => new ParamOption(kv.Key, kv.Value)).ToList(),
                Meta(ParameterMetaDataConstants.User),
                ParameterMetaDataRepository.GetParameterRebootRequired(name, firmware),
                bool.TryParse(Meta(ParameterMetaDataConstants.ReadOnly), out var ro) && ro);
        }
    }
}
