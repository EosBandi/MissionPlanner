using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.ArduPilot.Mavlink;
using MPBackend.Core.Links;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core.Files
{
    public sealed record FtpEntry(string Name, string Path, bool IsDirectory, ulong Size, DateTime? ModifiedUtc);

    public sealed class FtpException : Exception
    {
        public FtpException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// MAVLink FTP (the protocol behind MP's MAVFtp browser): list, read, write, delete, rename, mkdir, crc.
    /// Operations on one link are serialised and share one MP <see cref="MAVFtp"/> per vehicle (sequence numbers
    /// must keep increasing across operations).
    /// Paths are vehicle paths such as "/", "/APM/LOGS", "@SYS/uarts.txt", "@PARAM/param.pck".
    /// </summary>
    public sealed class FtpService
    {
        private readonly VehicleService vehicles;

        public FtpService(VehicleService vehicles)
        {
            this.vehicles = vehicles;
        }

        public Task<IReadOnlyList<FtpEntry>> ListAsync(uint sysid, byte compid, string path,
            CancellationToken ct = default) =>
            Run(sysid, compid, ct, (ftp, cts) =>
            {
                var dir = Normalise(path);
                return (IReadOnlyList<FtpEntry>)ftp.kCmdListDirectory(dir, cts)
                    .Where(f => f.Name != "." && f.Name != "..")
                    .Select(f => new FtpEntry(f.Name, Combine(dir, f.Name), f.isDirectory, f.Size, f.ModifiedUtc))
                    .OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

        /// <summary>
        /// Read a whole file. <paramref name="progress"/> receives 0-100.
        /// </summary>
        public Task<MemoryStream> DownloadAsync(uint sysid, byte compid, string path, Action<int> progress = null,
            CancellationToken ct = default) =>
            Run(sysid, compid, ct, progress, (ftp, cts) =>
            {
                var data = ftp.GetFile(Normalise(path), cts);
                if (data == null && !cts.IsCancellationRequested)
                    throw new FtpException($"failed to read {path}");
                cts.Token.ThrowIfCancellationRequested();
                data.Position = 0;
                return data;
            });

        public Task<bool> UploadAsync(uint sysid, byte compid, string path, Stream content,
            Action<int> progress = null, CancellationToken ct = default) =>
            Run(sysid, compid, ct, progress, (ftp, cts) =>
            {
                ftp.UploadFile(Normalise(path), content, cts);
                cts.Token.ThrowIfCancellationRequested();
                return true;
            });

        public Task<bool> DeleteAsync(uint sysid, byte compid, string path, bool directory, bool recursive,
            CancellationToken ct = default) =>
            Run(sysid, compid, ct, (ftp, cts) =>
                Check(directory
                    ? recursive ? ftp.RemoveDirectoryRecursive(Normalise(path), cts)
                    : ftp.kCmdRemoveDirectory(Normalise(path), cts)
                    : ftp.kCmdRemoveFile(Normalise(path), cts), "delete", path));

        public Task<bool> CreateDirectoryAsync(uint sysid, byte compid, string path, CancellationToken ct = default) =>
            Run(sysid, compid, ct, (ftp, cts) => Check(ftp.kCmdCreateDirectory(Normalise(path), cts), "mkdir", path));

        public Task<bool> RenameAsync(uint sysid, byte compid, string from, string to, CancellationToken ct = default) =>
            Run(sysid, compid, ct, (ftp, cts) =>
                Check(ftp.kCmdRename(Normalise(from), Normalise(to), cts), "rename", from));

        public Task<uint> Crc32Async(uint sysid, byte compid, string path, CancellationToken ct = default) =>
            Run(sysid, compid, ct, (ftp, cts) =>
            {
                uint crc = 0;
                Check(ftp.kCmdCalcFileCRC32(Normalise(path), ref crc, cts), "crc", path);
                return crc;
            });

        private Task<T> Run<T>(uint sysid, byte compid, CancellationToken ct, Func<MAVFtp, CancellationTokenSource, T> op) =>
            Run(sysid, compid, ct, null, op);

        private Task<T> Run<T>(uint sysid, byte compid, CancellationToken ct, Action<int> progress,
            Func<MAVFtp, CancellationTokenSource, T> op)
        {
            var (link, mav) = vehicles.Resolve(sysid, compid);
            if ((mav.cs.capabilities & (int)MAVLink.MAV_PROTOCOL_CAPABILITY.FTP) == 0)
                throw new FtpException($"vehicle {sysid}/{compid} does not support MAVFTP");

            return link.RunTransferAsync(m => Task.Run(() =>
            {
                // MAVFtp takes a CancellationTokenSource; bridge the request token into it
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var ftp = link.GetFtp(sysid, compid);
                Action<string, int> onProgress = (_, percent) => progress?.Invoke(percent);
                ftp.Progress += onProgress;
                try
                {
                    return op(ftp, cts);
                }
                finally
                {
                    ftp.Progress -= onProgress;
                }
            }, ct), ct);
        }

        private static bool Check(bool ok, string op, string path) =>
            ok ? true : throw new FtpException($"{op} failed for {path}");

        private static string Normalise(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";
            path = path.Replace('\\', '/');
            return path.StartsWith("/") || path.StartsWith("@") ? path : "/" + path;
        }

        private static string Combine(string dir, string name) =>
            dir.EndsWith("/") ? dir + name : dir + "/" + name;
    }
}
