using System;
using MPBackend.Core.Calibration;
using MPBackend.Core.Control;
using MPBackend.Core.Files;
using MPBackend.Core.Gps;
using MPBackend.Core.Jobs;
using MPBackend.Core.Links;
using MPBackend.Core.Maps;
using MPBackend.Core.Missions;
using MPBackend.Core.Params;
using MPBackend.Core.Planning;
using MPBackend.Core.Terrain;
using MPBackend.Core.Vehicles;

namespace MPBackend.Core
{
    /// <summary>
    /// Entry point for hosts: wires the headless hooks and owns all backend services.
    /// </summary>
    public sealed class Backend : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Backend));

        public Backend(BackendOptions options = null)
        {
            options ??= new BackendOptions();
            HeadlessHost.Initialise(options);
            Events = new EventHub();
            Jobs = new JobManager(Events);
            Links = new LinkManager(Events, Jobs);
            Vehicles = new VehicleService(Links);
            Params = new ParamService(Links, Vehicles, Jobs, Events, options);
            Ftp = new FtpService(Vehicles);
            Logs = new LogService(Vehicles, Jobs);
            Missions = new MissionService(Vehicles, Jobs, Events);
            Terrain = new TerrainService();
            Planning = new SprayGridService();
            Tiles = new TileService(Jobs, options);
            Calibration = new CalibrationService(Vehicles, Params, Events);
            ManualControl = new ManualControlService(Vehicles);
            Rtk = new RtkInjectService(Links, Events);
            Adsb = new AdsbService(Links, Events);

            // MainV2.doConnect: fetch params after connecting (not for peripherals without a param service)
            Links.Connected += link =>
            {
                var mav = link.Mav.MAV;
                if (!link.Request.FetchParams ||
                    mav.compid == (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_PERIPHERAL)
                    return;
                try
                {
                    Params.StartDownload(mav.sysid, mav.compid);
                }
                catch (Exception ex)
                {
                    log.Error("param download after connect", ex);
                }
            };
        }

        public EventHub Events { get; }
        public JobManager Jobs { get; }
        public LinkManager Links { get; }
        public VehicleService Vehicles { get; }
        public ParamService Params { get; }
        public FtpService Ftp { get; }
        public LogService Logs { get; }
        public MissionService Missions { get; }
        public TerrainService Terrain { get; }
        public SprayGridService Planning { get; }
        public TileService Tiles { get; }
        public CalibrationService Calibration { get; }
        public ManualControlService ManualControl { get; }
        public RtkInjectService Rtk { get; }
        public AdsbService Adsb { get; }

        public void Dispose()
        {
            ManualControl.Dispose();
            Rtk.Dispose();
            Adsb.Dispose();
            Links.Dispose();
            MissionPlanner.Utilities.Settings.Instance.Save();
        }
    }
}
