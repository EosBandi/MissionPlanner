using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Utilities;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Files
{
    public sealed record OnboardLog(ushort Id, uint Size, DateTime? TimeUtc);

    public sealed record LocalLogFile(string Path, string Kind, long Size, DateTime ModifiedUtc);

    /// <summary>
    /// Onboard dataflash log list/download/erase over LOG_* messages (MP's "Download DataFlash Log Via Mavlink"),
    /// plus access to the local log directory (downloaded .bin files and recorded .tlog files).
    /// </summary>
    public sealed class LogService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(LogService));
        private static readonly string[] LocalExtensions = { ".bin", ".tlog", ".rlog", ".log" };

        private readonly VehicleService vehicles;
        private readonly JobManager jobs;

        public LogService(VehicleService vehicles, JobManager jobs)
        {
            this.vehicles = vehicles;
            this.jobs = jobs;
        }

        public static string LogDirectory => Settings.Instance.LogDir;

        public async Task<IReadOnlyList<OnboardLog>> ListAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var (link, _) = vehicles.Resolve(sysid, compid);
            var entries = await link.RunExclusiveAsync(m =>
            {
                // MP's log list API targets the "current" vehicle on the link
                Select(m, sysid, compid);
                return m.GetLogEntry(0, ushort.MaxValue);
            }, ct).ConfigureAwait(false);

            return entries.Values.OrderBy(e => e.id)
                .Select(e => new OnboardLog(e.id, e.size,
                    e.time_utc > 0 ? DateTimeOffset.FromUnixTimeSeconds(e.time_utc).UtcDateTime : null))
                .ToList();
        }

        /// <summary>
        /// Download logs one after another into LogDir/{type}/{sysid}/. Cancelling takes effect between logs
        /// (MP's LOG_DATA loop cannot be interrupted cleanly).
        /// </summary>
        public Job StartDownload(uint sysid, byte compid, IReadOnlyList<ushort> ids, bool allowArmed = false)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            if (mav.cs.armed && !allowArmed)
                throw new InvalidOperationException("vehicle is armed; log download would load the link in flight");

            return jobs.Start("logs.download", $"Download {ids.Count} log(s) {sysid}/{compid}", async ctx =>
            {
                var all = await ListAsync(sysid, compid, ctx.Token).ConfigureAwait(false);
                var selected = all.Where(l => ids.Contains(l.Id)).ToList();
                var missing = ids.Except(selected.Select(l => l.Id)).ToList();
                if (missing.Count > 0)
                    throw new ArgumentException("no such log id: " + string.Join(",", missing));

                var totalBytes = Math.Max(1L, selected.Sum(l => (long)l.Size));
                long doneBytes = 0;
                var files = new List<string>();

                foreach (var entry in selected)
                {
                    ctx.Token.ThrowIfCancellationRequested();
                    var label = $"log {entry.Id} ({entry.Size / 1024} KiB)";
                    ProgressEventHandler onProgress = (offset, _) =>
                        ctx.Report((int)((doneBytes + offset) * 100 / totalBytes), label);

                    var temp = await link.RunTransferAsync(async m =>
                    {
                        m.Progress += onProgress;
                        try
                        {
                            return await m.GetLog(sysid, compid, entry.Id).ConfigureAwait(false);
                        }
                        finally
                        {
                            m.Progress -= onProgress;
                        }
                    }, ctx.Token).ConfigureAwait(false);

                    var target = TargetPath(mav, entry);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Move(temp, target, true);
                    files.Add(RelativeToLogDir(target));
                    doneBytes += entry.Size;
                    log.Info($"downloaded log {entry.Id} to {target}");
                }

                return new { files };
            }, link.Id);
        }

        /// <summary>
        /// Erase all onboard logs (LOG_ERASE has no acknowledgement; refresh the list to confirm).
        /// </summary>
        public Task EraseAllAsync(uint sysid, byte compid, CancellationToken ct = default)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            if (mav.cs.armed)
                throw new InvalidOperationException("vehicle is armed");
            return link.RunExclusiveAsync(m =>
            {
                Select(m, sysid, compid);
                m.EraseLog();
                return true;
            }, ct);
        }

        public IReadOnlyList<LocalLogFile> ListLocal()
        {
            var root = LogDirectory;
            if (!Directory.Exists(root))
                return Array.Empty<LocalLogFile>();

            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => LocalExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => new LocalLogFile(RelativeToLogDir(f.FullName), f.Extension.TrimStart('.').ToLowerInvariant(),
                    f.Length, f.LastWriteTimeUtc))
                .ToList();
        }

        /// <summary>
        /// Resolve a path returned by <see cref="ListLocal"/>; refuses anything outside the log directory.
        /// </summary>
        public static string ResolveLocal(string relativePath)
        {
            var root = Path.GetFullPath(LogDirectory);
            var full = Path.GetFullPath(Path.Combine(root, relativePath ?? ""));
            if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new FileNotFoundException("log file not found", relativePath);
            return full;
        }

        private static void Select(MAVLinkInterface m, uint sysid, byte compid)
        {
            m.sysidcurrent = sysid;
            m.compidcurrent = compid;
        }

        private static string TargetPath(MAVState mav, OnboardLog entry)
        {
            var dir = Path.Combine(LogDirectory, mav.aptype.ToString(), mav.sysid.ToString());
            // MP names by GPS start time read from the log; LOG_ENTRY's time_utc is the same information
            var name = entry.TimeUtc.HasValue && entry.TimeUtc.Value.Year >= 1990
                ? entry.TimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH-mm-ss")
                : $"{entry.Id} {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
            var path = Path.Combine(dir, Regex.Replace(name, "[^0-9A-Za-z _-]", "_") + ".bin");
            for (var n = 1; File.Exists(path); n++)
                path = Path.Combine(dir, $"{name}-{n}.bin");
            return path;
        }

        private static string RelativeToLogDir(string fullPath) =>
            Path.GetRelativePath(LogDirectory, fullPath).Replace('\\', '/');
    }
}
