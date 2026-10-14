using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

namespace MPBackend.Core
{
    public sealed class BackendOptions
    {
        /// <summary>
        /// Root for config.xml, logs, param cache, srtm, etc. Kept separate from an installed Mission Planner.
        /// </summary>
        public string DataDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MPBackend");

        /// <summary>
        /// Record .tlog/.rlog files for every link.
        /// </summary>
        public bool RecordTlogs { get; set; } = true;

        /// <summary>
        /// Reuse the per-vehicle param cache (MP's paramcache/param.json) if younger than this. Zero disables.
        /// </summary>
        public TimeSpan ParamCacheMaxAge { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Map tile cache root. Defaults to DataDirectory\gmapcache; point it at an existing Mission Planner
        /// gmapcache folder to reuse its tiles.
        /// </summary>
        public string MapCacheDirectory { get; set; }

        /// <summary>ServerAndCache (default), CacheOnly (offline) or ServerOnly.</summary>
        public GMap.NET.AccessMode MapAccessMode { get; set; } = GMap.NET.AccessMode.ServerAndCache;

        /// <summary>
        /// SRTM/DTED/GeoTIFF folder. Defaults to DataDirectory\srtm.
        /// </summary>
        public string SrtmDirectory { get; set; }

        /// <summary>
        /// log4net level for the rolling file log in DataDirectory\logs (DEBUG, INFO, WARN, ERROR).
        /// </summary>
        public string LogLevel { get; set; } = "INFO";
    }

    /// <summary>
    /// One-time process setup: replaces the WinForms hooks Mission Planner wires in Program.cs with
    /// headless equivalents. The libraries use static state, so there is one backend per process.
    /// </summary>
    public static class HeadlessHost
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(HeadlessHost));
        private static readonly object initLock = new object();
        private static bool initialised;

        /// <summary>
        /// Values handed to transports that read their endpoint from CommsBase settings (e.g. WS_url).
        /// </summary>
        internal static readonly ConcurrentDictionary<string, string> CommsOverrides =
            new ConcurrentDictionary<string, string>();

        public static BackendOptions Options { get; private set; } = new BackendOptions();

        public static void Initialise(BackendOptions options)
        {
            lock (initLock)
            {
                if (initialised)
                    return;
                initialised = true;
                Options = options;

                Directory.CreateDirectory(options.DataDirectory);
                ConfigureLogging(options);

                // Settings appends AppConfigName ("Mission Planner") to this path
                Settings.CustomUserDataDirectory = options.DataDirectory;
                Settings.Instance.Load();
                // used by srtm and map tile downloads (MP sets "Mission Planner <version> (<os>)")
                Settings.Instance.UserAgent = "MPBackend " +
                                              typeof(HeadlessHost).Assembly.GetName().Version + " (" +
                                              Environment.OSVersion.VersionString + ")";

                srtm.datadirectory = options.SrtmDirectory ?? Path.Combine(options.DataDirectory, "srtm");
                Directory.CreateDirectory(srtm.datadirectory);

                // the libs throw if no message box handler is attached; answer with the safe default
                CustomMessageBox.ShowEvent += (text, caption, buttons, icon, yesText, noText) =>
                {
                    log.Warn($"messagebox suppressed: {caption}: {text}");
                    switch (buttons)
                    {
                        case CustomMessageBox.MessageBoxButtons.YesNo:
                        case CustomMessageBox.MessageBoxButtons.YesNoCancel:
                            return CustomMessageBox.DialogResult.No;
                        case CustomMessageBox.MessageBoxButtons.OKCancel:
                        case CustomMessageBox.MessageBoxButtons.RetryCancel:
                            return CustomMessageBox.DialogResult.Cancel;
                        case CustomMessageBox.MessageBoxButtons.AbortRetryIgnore:
                            return CustomMessageBox.DialogResult.Abort;
                        default:
                            return CustomMessageBox.DialogResult.OK;
                    }
                };

                // never prompt; transports must be fully configured by the caller
                CommsBase.InputBoxShow += (string title, string prompt, ref string text) => inputboxreturn.NotSet;

                // returning "" makes the transport use the value it was given instead of a persisted one
                CommsBase.Settings += (name, value, set) =>
                    !set && CommsOverrides.TryGetValue(name, out var v) ? v : "";

                MAVLinkInterface.CreateIProgressReporterDialogue += title => new HeadlessProgressReporter(title);

                log.Info($"headless host initialised, data dir {Settings.GetUserDataDirectory()}");
            }
        }

        /// <summary>
        /// The MP libraries log through log4net; send it to a rolling file plus warnings to the console.
        /// </summary>
        private static void ConfigureLogging(BackendOptions options)
        {
            var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository(
                System.Reflection.Assembly.GetEntryAssembly() ?? typeof(HeadlessHost).Assembly);
            if (hierarchy.Configured)
                return;

            var layout = new log4net.Layout.PatternLayout("%date [%thread] %-5level %logger - %message%newline");
            layout.ActivateOptions();

            var file = new log4net.Appender.RollingFileAppender
            {
                File = Path.Combine(options.DataDirectory, "logs", "mpbackend.log"),
                AppendToFile = true,
                RollingStyle = log4net.Appender.RollingFileAppender.RollingMode.Size,
                MaxSizeRollBackups = 5,
                MaximumFileSize = "10MB",
                StaticLogFileName = true,
                Layout = layout
            };
            file.ActivateOptions();

            var console = new log4net.Appender.ConsoleAppender
            {
                Layout = layout,
                Threshold = log4net.Core.Level.Warn
            };
            console.ActivateOptions();

            hierarchy.Root.AddAppender(file);
            hierarchy.Root.AddAppender(console);
            hierarchy.Root.Level = hierarchy.LevelMap[options.LogLevel?.ToUpperInvariant() ?? "INFO"] ??
                                   log4net.Core.Level.Info;
            hierarchy.Configured = true;
        }

        /// <summary>
        /// Replacement for the WinForms ProgressReporterDialogue: runs the work inline and logs progress.
        /// </summary>
        private sealed class HeadlessProgressReporter : IProgressReporterDialogue
        {
            private readonly string title;

            public HeadlessProgressReporter(string title)
            {
                this.title = title;
            }

            public event DoWorkEventHandler DoWork;

            public ProgressWorkerEventArgs doWorkArgs { get; set; } = new ProgressWorkerEventArgs();

            public void BeginInvoke(Delegate method) => method?.DynamicInvoke();

            public void Dispose()
            {
            }

            public void RunBackgroundOperationAsync()
            {
                Task.Run(() =>
                {
                    try
                    {
                        DoWork?.Invoke(this);
                    }
                    catch (Exception ex)
                    {
                        doWorkArgs.ErrorMessage = ex.Message;
                        log.Error(title, ex);
                    }
                }).Wait();
            }

            public void UpdateProgressAndStatus(int progress, string status) =>
                log.Info($"{title}: {progress}% {status}");
        }
    }
}
