# Mission Planner headless backend

A GUI-less backend built from Mission Planner's own comms and vehicle code, exposed as REST + WebSocket for a
web (browser/Electron) GCS. Windows, .NET 8. Nothing in Mission Planner itself is modified.

## Quick start

```bash
dotnet run --project Headless/MPBackend.Server
```

Open `http://127.0.0.1:5880/dev.html` (dev console: links, telemetry, map, gamepad) or `/swagger` (API).
For a simulator, `Headless/tools/Start-Sitl.ps1 -Restart` runs the ArduCopter SITL that Mission Planner's
Simulation screen downloads, on tcp:5760.

Configuration (`appsettings.json` or command line):

| Setting | Default | |
|---|---|---|
| `--urls` | `http://127.0.0.1:5880` | `http://0.0.0.0:5880` for LAN access. There is **no auth/TLS**; only expose it on a trusted network. |
| `MPBackend:DataDirectory` | `%LOCALAPPDATA%\MPBackend` | config.xml, logs, tlogs, param cache, srtm, map cache. Separate from an installed Mission Planner. |
| `MPBackend:AutoConnect:0` | – | e.g. `tcp:127.0.0.1:5760`, connected at startup |
| `MPBackend:RecordTlogs` | `true` | `.tlog`/`.rlog` per link in the log directory |
| `MPBackend:ParamCacheMaxAge` | `01:00:00` | reuse the param cache if younger |
| `MPBackend:MapCacheDirectory` | `<data>\gmapcache` | point at an existing Mission Planner `gmapcache` to reuse its tiles |
| `MPBackend:MapAccessMode` | `ServerAndCache` | `CacheOnly` for offline maps |
| `MPBackend:SrtmDirectory` | `<data>\srtm` | SRTM/DTED/GeoTIFF terrain |
| `MPBackend:LogLevel` | `INFO` | log4net level for `<data>\logs\mpbackend.log` |

## Layout

| Path | What it is |
|---|---|
| `Libs/MPBackend.Utilities`, `Libs/MPBackend.ArduPilot` | Slim builds of `ExtLibs/Utilities` and `ExtLibs/ArduPilot`. They compile the **original source files by link** (nothing copied or edited), minus imaging/video/scripting/GUI pieces (SkiaSharp, ImageSharp, LibVLC, Roslyn, SharpDX joystick, Dokan, dataflash log analysis). |
| `MPBackend.Core` | The headless orchestration that `MainV2` and the setup screens do in Mission Planner. No web dependencies; a .NET GUI could use it directly via the `Backend` class. |
| `MPBackend.Server` | ASP.NET Core host: REST under `/api/v1`, WebSocket at `/ws`, Swagger, dev console. |
| `MPBackend.Tests` | Unit tests plus a SITL end-to-end test (category `SITL`, inconclusive without SITL). |
| `client-ts` | `@mpbackend/client`: generated TypeScript types + typed REST client + WebSocket client. |
| `tools/Start-Sitl.ps1` | SITL launcher for development. |

`MPBackend.Core` services (`Backend.*`): `Links`, `Vehicles`, `Params`, `Missions`, `Ftp`, `Logs`, `Terrain`,
`Tiles`, `Calibration`, `ManualControl`, `Rtk`, `Adsb`, `Jobs`, `Events`.

## API overview

All REST paths are under `/api/v1`; vehicles are addressed as `vehicles/{sysid}/{compid}` whatever link they are
on. Long operations return or run as **jobs** (`GET/DELETE jobs/{id}`, progress on the `jobs` topic).
Errors are RFC 7807 problem documents (404 unknown vehicle/file, 409 conflicting state, 502 vehicle/IO failure).

### Connections and vehicles

`POST links {"connection": "..."}` with `tcp:host:port`, `udp:14550` (listen), `udpcl:host:port`,
`COM3:115200` or `ws:ws://host/path`. It returns the link (`Connecting`) and a connect job; after the
heartbeat/version handshake the parameter list is downloaded as a separate `params.download` job.
`GET links`, `DELETE links/{id}`, `GET serialports`, `GET vehicles`.

Per vehicle: `GET` (info), `GET state?fields=` (CurrentState snapshot), `GET messages` (STATUSTEXT history),
`GET modes`, `POST mode`, `POST arm`, `POST takeoff`, `POST goto` (guided), `POST reboot`,
`POST command` (any MAV_CMD by spec name, e.g. `MAV_CMD_NAV_RETURN_TO_LAUNCH`, or number; COMMAND_LONG or INT).
`GET telemetry/fields` lists all ~1000 CurrentState fields with type, display text and group.

### Parameters, MAVFTP, logs

| Area | Endpoints (per vehicle) |
|---|---|
| Parameters | `GET params[?metadata=true]`, `GET/PUT params/{name}`, `POST params/write` (bulk, job), `POST params/refresh` (job), `GET params/metadata?names=`, `GET params/file` (.param), `POST params/compare` (.param as raw body or multipart `file`) |
| MAVFTP | `GET ftp/list?path=`, `GET/PUT/DELETE ftp/file?path=`, `DELETE ftp/dir?path=&recursive=`, `POST ftp/mkdir`, `POST ftp/rename`, `GET ftp/crc?path=` |
| Logs | `GET logs` (onboard), `POST logs/download` (job), `DELETE logs` (erase all) |

Global: `POST params/metadata/update`, `POST params/parse`, `GET logs/local`, `GET logs/local/file?path=`.

- Param download order: local cache (younger than `ParamCacheMaxAge`), then MAVFTP `@PARAM/param.pck`
  (includes defaults), then `PARAM_REQUEST_LIST`. apm.pdef.xml metadata for the firmware version is fetched
  afterwards. `.param` files may be in MP, MAVProxy or QGC format; volatile parameters are skipped as in MP.
- Log downloads go to `<logs>/{type}/{sysid}/`, named by the log's UTC start time; refused while armed unless
  `allowArmed`; cancelling takes effect between logs.

### Missions, terrain, maps

| Area | Endpoints |
|---|---|
| Missions (`{type}` = mission, fence, rally) | `GET vehicles/{s}/{c}/missions/{type}[?format=waypoints\|plan\|kml]`, `PUT .../missions/{type}`, `POST .../missions/{type}/file`, `DELETE .../missions/{type}`, `POST .../missions/current` |
| Mission files | `POST missions/parse`, `POST missions/export?format=` |
| Terrain | `GET terrain/elevation?lat&lng&wait`, `POST terrain/profile`, `GET terrain/geoid?lat&lng` |
| Maps | `GET maps/providers`, `GET maps/tiles/{provider}/{z}/{x}/{y}`, `POST maps/prefetch` (job), `GET/PUT maps/mode` |

- A plan is `{type, home, items[]}`; items use raw MAVLink units with `command` or `commandName`, `frame`,
  `param1-4`, `lat`, `lng`, `alt`. ArduPilot's mission item 0 (home) is returned as `home` and inserted on
  upload (the plan's `home`, else the vehicle's). Fence items are sent in the GLOBAL frame.
- `.waypoints` = MP's QGC WPL 110 (all three types); `.plan` = QGC JSON with per-item frames, geofence and
  rally; KML import turns LineStrings into waypoints.
- Terrain: MP's `srtm` (GeoTIFF/DTED first, SRTM tiles downloaded on demand). Vehicle TERRAIN_REQUESTs are
  answered automatically.
- Tiles: GMap.NET's providers, e.g. Leaflet `L.tileLayer('/api/v1/maps/tiles/BingSatelliteMap/{z}/{x}/{y}')`.

### Calibration and manual control

| Area | Endpoints (per vehicle) |
|---|---|
| Calibration | `GET calibration`, `POST calibration/accel/start`, `POST calibration/accel/next`, `POST calibration/preflight/{level\|simpleaccel\|gyro\|baro\|airspeed}`, `POST calibration/compass/start`, `POST calibration/compass/accept`, `POST calibration/compass/fixed-yaw`, `POST calibration/radio/start`, `POST calibration/radio/finish`, `POST calibration/cancel` |
| Manual control | `POST manual-control`, `GET manual-control`, `DELETE manual-control` |

- **Accel**: the vehicle requests each position (`state: WaitingForUser`, `position: LEVEL|LEFT|…`); call
  `accel/next` once placed. **Compass**: per-compass progress and reports; autosave on; reboot afterwards.
  **Radio**: `radio/start` streams live `channels`; move everything to its extremes, centre with throttle down,
  then `radio/finish` writes `RCx_MIN/MAX/TRIM`. One calibration per vehicle at a time (`calibration` topic).
- **Manual control** (browser Gamepad API): WebSocket frames at 10–50 Hz,
  `{"op":"manual_control","sysid":1,"compid":1,"normalized":{"1":0,"2":0,"3":-1,"4":0}}`. `normalized` maps
  -1..1 with `RCx_MIN/TRIM/MAX` (throttle linearly min..max); or raw PWM `channels` (null = untouched,
  0 = release) or `manual` (MANUAL_CONTROL). Sent to the vehicle at 20 Hz; **released 500 ms after the last
  frame** so the RC receiver takes over. `dev.html` has a Mode 2 example.

### RTK, ADS-B, settings

| Area | Endpoints |
|---|---|
| RTK injection | `GET rtk`, `POST rtk/start`, `POST rtk/stop`, `POST rtk/ublox/survey-in`, `POST rtk/ublox/fixed` |
| ADS-B | `GET adsb/aircraft`, `GET/PUT adsb/config` |
| Settings | `GET settings`, `GET/PUT/DELETE settings/{key}` |

- RTK sources: `Ntrip` (`url: ntrip://user:pass@host:port/mount`, `ntripV1`), `Serial` (`port`, `baud`),
  `Tcp`, `Udp`, `UdpClient`. RTCM3 and SBP frames are injected into every vehicle (GPS_RTCM_DATA); status gives
  rates, per-message counts, last-seen per constellation, the base position from RTCM 1005/1006 and u-blox
  survey-in progress. The NTRIP GGA position follows the vehicle. Credentials are never echoed back.
- ADS-B: aircraft reported by vehicles plus an optional external feed (`server` = dump1090 host with SBS/AVR
  `port`, or an ADSBExchange-v2 API URL such as `https://api.adsb.lol`). Units as in MP (alt m, speed cm/s).
  The nearest external aircraft are forwarded to the vehicle as ADSB_VEHICLE (`forwardToVehicle`).
- Settings is Mission Planner's key/value store (`config.xml` in the data directory); the GUI can keep its own
  preferences there. Keys that look secret (`pass`, `token`, `apikey`, …) are masked.

### WebSocket (`/ws`)

Client → server:

```json
{"op":"subscribe","topic":"links"}
{"op":"subscribe","topic":"telemetry","sysid":1,"compid":1,"fields":["alt","roll"],"rate":10}
{"op":"subscribe","topic":"mavlink","sysid":1,"msgs":["ATTITUDE","GPS_RAW_INT"]}
{"op":"unsubscribe","topic":"telemetry","sysid":1,"compid":1}
{"op":"manual_control","sysid":1,"compid":1,"normalized":{"3":-1}}
{"op":"manual_control_stop","sysid":1,"compid":1}
{"op":"ping"}
```

Server → client `{"topic": ..., "data": ...}`. Topics: `links`, `vehicles`, `status`, `jobs`, `params`,
`missions`, `calibration`, `rtk`, `adsb`, `telemetry` (adds `sysid`, `compid`, `full`; `data` holds only changed
fields, with a full snapshot first and every 5 s) and `mavlink` (adds `linkId`, `sysid`, `compid`, `msg`).
Slow clients drop the oldest frames instead of blocking the backend.

## TypeScript client

```bash
cd Headless/client-ts && npm install && npm run build
```

```ts
import { createBackendClient, BackendSocket, waitForJob } from "@mpbackend/client";

const api = createBackendClient("http://127.0.0.1:5880");
const { data } = await api.POST("/api/v1/links", { body: { connection: "tcp:127.0.0.1:5760" } });
await waitForJob(api, data!.job.id);

const ws = new BackendSocket("http://127.0.0.1:5880");     // reconnects and re-subscribes
ws.on("status", s => console.log(s.severity, s.text));
ws.telemetry(1, 1, state => hud.update(state), { fields: ["roll", "pitch", "alt"], rate: 20 });
ws.manualControl(1, 1, { normalized: { 3: -1 } });          // call at 10-50 Hz from a gamepad loop
```

After changing the API: start the server, `npm run fetch-spec && npm run generate`. `node scripts/smoke.mjs`
exercises the client against a running backend + vehicle.

## Tests

`dotnet test Headless/MPBackend.Tests` runs the unit tests and, if Mission Planner's SITL is installed, an
end-to-end test (tcp:5790, so a dev SITL on 5760 can keep running): connect, params (set, export, compare),
MAVFTP, logs, mission/fence round trips, arm/takeoff/land, RC override driven radio calibration.
CI (`.github/workflows/headless.yml`) builds the solution, runs the non-SITL tests and checks the TypeScript
client is in sync with `openapi.json`.

## Keeping up with upstream Mission Planner

The slim projects include `ExtLibs/Utilities` and `ExtLibs/ArduPilot` sources by wildcard with an explicit
exclusion list, so upstream fixes arrive with a normal merge. If upstream adds a file that needs an excluded
library, add it to the `Compile Remove` list in the slim csproj; if kept code starts using an excluded file,
the build fails loudly. The orchestration ported from GUI code (`MainV2` reader/connect, FlightPlanner mission
handling, calibration screens, GPS inject, ADS-B, joystick loop) does not update automatically; the class
comments name the Mission Planner method each part was ported from.

## Design notes

- MP's request/response APIs (params, missions, commands) take over the port via `giveComport` and read it
  themselves. `Link.RunExclusiveAsync` serialises them per link and the reader loop yields while they run.
- MAVFTP and LOG_DATA transfers are driven by the reader loop through packet subscriptions; they take a separate
  per-link transfer lock so a long download does not block parameter writes or commands.
- One `MAVFtp` instance is kept per vehicle per link: ArduPilot answers a request that repeats the previous
  sequence number with its cached reply, which MP then ignores.
- `CustomMessageBox` prompts inside the libraries are answered with the safe default (No/Cancel/OK) and logged.
- The libraries use static state (Settings, hooks, srtm, GMap), so it is one backend per process.

## Known limitations

- Not ported: firmware flashing/board detection, dataflash log analysis/graphs, tlog playback, video, speech,
  DirectInput joystick (use the browser Gamepad API), DroneCAN, follow-me, survey/grid generation.
- Accel and compass calibration need a physical vehicle (verified only for the message flow in SITL).
- A terrain-following (GLOBAL_TERRAIN_ALT) mission item made ArduPilot 4.7 SITL refuse to arm with
  "terrain data expired" even though all terrain blocks were loaded; check on hardware.
- MP's own MAVFTP param download creates a fresh FTP session (sequence numbers restart); it runs first after
  connecting and has not collided in testing.

## GCS app (MAUI Blazor Hybrid, Windows + Android)

`MissionPlanner.GCS.slnx` adds two projects on top of the backend (they need the .NET 10 SDK with the `android` and
`maui-windows` workloads, so they are kept out of the net8 `MissionPlanner.Headless.slnx` that CI builds):

| Path | What it is |
|---|---|
| `MPBackend.UI` | Razor class library with the screens. All backend access goes through `IGcs`; `InProcessGcs` wraps `Backend` directly (a REST/WebSocket implementation can replace it for a browser build). `wwwroot/css/tokens.css` is generated from the glass-cockpit design spec by `tools/gen-tokens.py <gcs-design-spec.json>`. |
| `MPBackend.App` | .NET MAUI Blazor Hybrid host (`net10.0-windows`, `net10.0-android`). Creates the `Backend` in-process with its data in the app's private directory and shows `GcsShell` in a WebView. |

```bash
dotnet build Headless/MPBackend.App -f net10.0-windows10.0.19041.0     # then run bin/Debug/.../MPBackend.App.exe
dotnet build Headless/MPBackend.App -f net10.0-android -t:Run          # device or emulator attached
```

`MPGCS_AUTOCONNECT=tcp:127.0.0.1:5760` in the environment opens a link at startup (development aid).

Layout follows the spec's variants (desktop PFD | MFD with two soft-key bars, tablet landscape/portrait, phone
composite with a PFD/MAP toggle key). The MFD page groups MAP / MSN / VEH / AUX and their pages are registered in
`Displays/PageRegistry.cs`; unimplemented pages show a placeholder. Keyboard: ←/→ page, Ctrl+←/→ page group, hold
ESC ≥ 1 s = Nav Map 1. Critical soft keys (ARM, AUTO, GUIDED) need a 1 s hold.

### Flight display

`MPBackend.UI/Pfd` is the glass-cockpit PFD from the design spec as Razor SVG components (800 × 640 reference
units, letterboxed): attitude with pitch ladder, roll scale, flight director and flight-path marker; speed and
altitude tapes with colour bands, trend vectors, selected-value bugs and the altitude alerter; VSI with limit bands;
HSI with turn-rate trend, course pointer / CDI, track diamond, heading bug and HOME bearing pointer; FMA; nav status
bar; wind / OAT / flight-time / HOME windows; VDI. `PfdFeed` samples the active vehicle at 20 Hz and each instrument
only re-renders when the values it draws change. Profiles (`VehicleProfile`) take their thresholds from the vehicle
parameters (WPNAV_SPEED, PILOT_SPEED_UP, ANGLE_MAX, AIRSPEED_MIN/MAX, TECS_*, FENCE_*, …) once they are downloaded.
`tools/gen-fma.py` regenerates the mode → FMA table from the spec.

`MPGCS_DEV_FLY=30` together with `MPGCS_AUTOCONNECT` flies a short GUIDED pattern on a SITL for testing.

### Alerts (CAS)

`MPBackend.UI/Alerts` implements the crew alerting system from the spec (`cas`, `ardupilot.alerts`): the alert
table is generated by `tools/gen-alerts.py`; `AlertService` evaluates the conditions it can observe (battery
voltage / mAh against BATT_* parameters, link age and quality, GPS fix / HDOP / satellites, EKF variances against
FS_EKF_THRESH, compass, airspeed and RC-receiver health from SYS_STATUS, vibration and clipping, fence breach,
terrain data pending, takeoff inhibit, simulation) and maps STATUSTEXT to CAS messages (PreArm, EKF / radio
failsafe, thrust loss, yaw imbalance, generic messages by severity). Levels, ordering, inverse video until
acknowledged, caution → warning re-acknowledgement, the connect policy, the takeoff / landing / on-ground inhibits
and the flight-phase chip follow the spec. Master lamps in the status strip (click to acknowledge), the CAS window
in the MFD corner (7 lines, 3 on phone, overflow indicator), the right-most ALERTS soft key (level text, flashing
until the window opens) and the alerts window (ACK, RCL history, CLOSE; key `K` acknowledges) complete the loop.
No aural alerts yet.

### Nav map

`MPBackend.UI/Map` + `Pages/NavMapPage.razor` implement MAP › NAV MAP 1 (spec mfd.navMap) without a JavaScript map
library: Web Mercator tiles come from the backend's `TileService` (GMap.NET providers and cache) as data-URI
images laid out in C#, with an SVG overlay for ownship (quad-cross / aircraft, heading, magenta track vector),
home, mission legs (completed gray, active magenta, upcoming white, RTL dashed), waypoints, rally points and
inclusion / exclusion fences. Range steps per profile (label = top-to-bottom distance), NORTH UP / TRK UP / HDG UP
orientation, declutter levels, drag or tap for the pan cursor with a lat/lon · BRG · DIS · ETE plate, MEASURE,
FLY HERE (hold, guided goto), range ring, mouse wheel zoom, and a MAP sub-menu for the tile provider.
`MPGCS_MAPCACHE=<gmapcache dir>` reuses an existing Mission Planner tile cache during development.

### Active mission page

MSN › ACTIVE MISSION (`Pages/MissionPage.razor`, spec mfd.mission): one row per mission item with command, altitude
and frame, DTK, DIS and ETE (the active leg measured from the vehicle, the others leg to leg, at the current
ground speed or the profile's cruise speed when stationary), remaining distance and time, the active row in magenta
with the → marker and a cyan cursor row (UP / DOWN keys, ↑ / ↓, or tap). SET WP (hold) sends MISSION_SET_CURRENT
for the cursor row; PAUSE / CONTINUE send MAV_CMD_DO_PAUSE_CONTINUE in AUTO; RELOAD re-downloads the list.

### Parameters page

AUX › PARAMETERS (`Pages/ParametersPage.razor`): the vehicle's full list in a virtualised table with the apm.pdef.xml
metadata (display name, description on tap, units, range, options as a drop-down, bitmask bits, reboot marker).
Search ranks name prefixes, then name matches, then description mentions. Edits are held as pending (cyan) until
WRITE (hold) sends them as one backend job and marks each row WRITTEN or FAILED; REVERT drops them, MODIFIED filters
to them, REFRESH re-downloads the list, EXPORT saves a Mission Planner .param file under the app's data directory,
and COMPARE loads a .param / .parm / QGC file, lists the differences against the vehicle and APPLY FILE (hold)
writes them. Copter 4.7's renamed m/s parameters (WP_SPD, LOIT_SPEED_MS, PILOT_SPD_UP, LAND_SPD_MS, ATC_ANGLE_MAX)
are understood by the vehicle profile alongside the older cm/s names.
