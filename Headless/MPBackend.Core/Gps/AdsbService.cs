using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Utilities;
using MPBackend.Core.Links;

namespace MPBackend.Core.Gps
{
    /// <param name="Source">"vehicle" (ADSB_VEHICLE from the autopilot) or "external" (SBS/AVR/HTTP feed).</param>
    public sealed record Aircraft(
        string Icao,
        string CallSign,
        double Lat,
        double Lng,
        double Alt,
        float Heading,
        double Speed,
        double VerticalSpeed,
        ushort Squawk,
        string ThreatLevel,
        string Source,
        DateTime LastSeen,
        double? Distance);

    public sealed class AdsbConfig
    {
        /// <summary>Read an external feed (in addition to ADSB_VEHICLE reported by vehicles).</summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Host of a dump1090-style SBS (port 30003) / AVR (30002) / Beast feed, or an http(s) base URL of an
        /// ADSBExchange v2 compatible API (e.g. https://api.adsb.lol), which is queried around the vehicle.
        /// </summary>
        public string Server { get; set; }

        public int Port { get; set; } = 30003;

        /// <summary>Send nearby external aircraft to the vehicle as ADSB_VEHICLE (MainV2.ADSBRunner).</summary>
        public bool ForwardToVehicle { get; set; } = true;
    }

    /// <summary>
    /// ADS-B traffic: merges aircraft reported by vehicles and from an external receiver/API, expires them after
    /// 30 s, and forwards the nearest external ones to the vehicle for avoidance. Port of MainV2's adsbPlanes,
    /// adsb_UpdatePlanePosition and ADSBRunner.
    /// </summary>
    public sealed class AdsbService : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(AdsbService));
        private static readonly TimeSpan Expiry = TimeSpan.FromSeconds(30);

        private readonly LinkManager links;
        private readonly EventHub events;
        private readonly ConcurrentDictionary<string, adsb.PointLatLngAltHdg> planes = new();
        private readonly CancellationTokenSource cts = new();
        private readonly object sync = new();
        private adsb feed;
        private int forwardIndex;

        public AdsbService(LinkManager links, EventHub events)
        {
            this.links = links;
            this.events = events;

            Config = new AdsbConfig
            {
                Enabled = Settings.Instance.GetBoolean("adsb", false),
                Server = Settings.Instance["adsbserver"],
                Port = Settings.Instance.GetInt32("adsbport", 30003)
            };

            adsb.ApplicationVersion = typeof(AdsbService).Assembly.GetName().Version?.ToString();
            adsb.UpdatePlanePosition += OnPlane;
            MAVLinkInterface.UpdateADSBPlanePosition += OnPlane;
            MAVLinkInterface.UpdateADSBCollision += OnCollision;

            if (Config.Enabled)
                StartFeed();
            _ = Task.Run(() => RunAsync(cts.Token));
        }

        public AdsbConfig Config { get; private set; }

        public AdsbConfig Configure(AdsbConfig config)
        {
            lock (sync)
            {
                Config = config;
                Settings.Instance["adsb"] = config.Enabled.ToString();
                Settings.Instance["adsbserver"] = config.Server ?? "";
                Settings.Instance["adsbport"] = config.Port.ToString(CultureInfo.InvariantCulture);
                Settings.Instance.Save();

                StopFeed();
                if (config.Enabled)
                    StartFeed();
                return Config;
            }
        }

        public IReadOnlyList<Aircraft> List()
        {
            var here = VehicleLocation();
            return planes.Values.Select(p => ToAircraft(p, here))
                .OrderBy(a => a.Distance ?? double.MaxValue).ToList();
        }

        private void StartFeed()
        {
            if (string.IsNullOrWhiteSpace(Config.Server))
                return;
            adsb.server = Config.Server;
            adsb.serverport = Config.Port;
            feed = new adsb();
            log.Info($"adsb feed {Config.Server}:{Config.Port}");
        }

        private void StopFeed()
        {
            if (feed == null)
                return;
            // MP's Stop uses Thread.Abort (unsupported on .NET 8); clearing run ends its loop instead
            adsb.Stop();
            feed = null;
        }

        /// <summary>MainV2.adsb_UpdatePlanePosition: vehicle reports never override a locally received aircraft.</summary>
        private void OnPlane(object sender, adsb.PointLatLngAltHdg update)
        {
            var id = update.Tag;
            if (string.IsNullOrEmpty(id))
                return;
            planes.AddOrUpdate(id,
                _ => new adsb.PointLatLngAltHdg(update.Lat, update.Lng, update.Alt, update.Heading, update.Speed, id,
                    DateTime.Now)
                {
                    CallSign = update.CallSign, Squawk = update.Squawk, Raw = update.Raw, Source = sender,
                    VerticalSpeed = update.VerticalSpeed
                },
                (_, plane) =>
                {
                    if (plane.Source == null && sender != null)
                        return plane;
                    plane.Lat = update.Lat;
                    plane.Lng = update.Lng;
                    plane.Alt = update.Alt;
                    plane.Heading = update.Heading;
                    plane.Time = DateTime.Now;
                    plane.CallSign = update.CallSign;
                    plane.Squawk = update.Squawk;
                    plane.Raw = update.Raw;
                    plane.Speed = update.Speed;
                    plane.VerticalSpeed = update.VerticalSpeed;
                    plane.Source = sender;
                    return plane;
                });
        }

        private void OnCollision(object sender, (string id, MAVLink.MAV_COLLISION_THREAT_LEVEL threat_level) e)
        {
            if (planes.TryGetValue(e.id, out var plane))
                plane.ThreatLevel = e.threat_level;
        }

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);

                    foreach (var stale in planes.Where(p => p.Value.Time < DateTime.Now - Expiry).Select(p => p.Key))
                        planes.TryRemove(stale, out _);

                    var here = VehicleLocation();
                    if (here != null)
                        adsb.CurrentPosition = here; // centre for HTTP API queries

                    if (Config.ForwardToVehicle && here != null)
                        Forward(here);

                    if (!planes.IsEmpty)
                        events.Publish(Topics.Adsb, List());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }
        }

        /// <summary>
        /// One aircraft per second, round robin over the 10 nearest external aircraft within 10 km (MainV2.ADSBRunner).
        /// </summary>
        private void Forward(PointLatLngAlt here)
        {
            var relevant = planes.Values
                .Where(p => !(p.Source is MAVLinkInterface))
                .Select(p => (p, d: p.GetDistance(here)))
                .Where(x => x.d <= 10000)
                .OrderBy(x => x.d)
                .Select(x => x.p)
                .Take(10)
                .ToList();
            if (relevant.Count == 0)
                return;
            forwardIndex = (forwardIndex + 1) % relevant.Count;
            var plane = relevant[forwardIndex];

            var packet = new MAVLink.mavlink_adsb_vehicle_t
            {
                altitude = (int)(plane.Alt * 1000),
                altitude_type = (byte)MAVLink.ADSB_ALTITUDE_TYPE.GEOMETRIC,
                callsign = (plane.CallSign ?? "").MakeBytes(),
                squawk = plane.Squawk,
                emitter_type = (byte)MAVLink.ADSB_EMITTER_TYPE.NO_INFO,
                heading = (ushort)(plane.Heading * 100),
                lat = (int)(plane.Lat * 1e7),
                lon = (int)(plane.Lng * 1e7),
                hor_velocity = (ushort)plane.Speed,
                ver_velocity = (short)plane.VerticalSpeed,
                ICAO_address = uint.TryParse(plane.Tag, NumberStyles.HexNumber, null, out var icao) ? icao : 0,
                flags = (ushort)(MAVLink.ADSB_FLAGS.VALID_ALTITUDE | MAVLink.ADSB_FLAGS.VALID_COORDS |
                                 MAVLink.ADSB_FLAGS.VALID_VELOCITY | MAVLink.ADSB_FLAGS.VALID_HEADING |
                                 MAVLink.ADSB_FLAGS.VALID_CALLSIGN)
            };

            foreach (var link in links.Links.Where(l => l.State == LinkState.Connected))
            {
                try
                {
                    link.Mav.sendPacket(packet, link.Mav.MAV.sysid, link.Mav.MAV.compid);
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }
        }

        private PointLatLngAlt VehicleLocation()
        {
            var cs = links.Links.Where(l => l.State == LinkState.Connected)
                .SelectMany(l => l.Vehicles).Select(v => v.cs)
                .FirstOrDefault(c => c.lat != 0 || c.lng != 0);
            return cs?.Location;
        }

        private static Aircraft ToAircraft(adsb.PointLatLngAltHdg p, PointLatLngAlt here) => new Aircraft(
            p.Tag, p.CallSign?.Trim(), p.Lat, p.Lng, p.Alt, p.Heading, p.Speed, p.VerticalSpeed, p.Squawk,
            p.ThreatLevel.ToString(), p.Source is MAVLinkInterface ? "vehicle" : "external", p.Time.ToUniversalTime(),
            here == null ? null : Math.Round(p.GetDistance(here)));

        public void Dispose()
        {
            cts.Cancel();
            adsb.UpdatePlanePosition -= OnPlane;
            MAVLinkInterface.UpdateADSBPlanePosition -= OnPlane;
            MAVLinkInterface.UpdateADSBCollision -= OnCollision;
            StopFeed();
        }
    }
}
