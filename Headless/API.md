# Headless backend API reference for GCS developers

This is the developer reference for the REST and WebSocket API served by `MPBackend.Server` (console) and
`MPBackend.Tray` (Windows tray app). Both hosts serve exactly the same API; everything below applies to either.
`README.md` has the backend overview, configuration and the per-feature notes; this document is about the wire
protocol a GCS user interface talks to.

The authoritative machine-readable spec is the OpenAPI document the running backend serves at
`/swagger/v1/swagger.json` (interactive UI at `/swagger`). `client-ts/` contains a typed TypeScript client
generated from it plus a WebSocket client; `wwwroot/dev.html` is a small reference UI that uses every part of
the protocol described here.

Contents

1. [Conventions](#1-conventions)
2. [Quick start: from zero to a flying vehicle](#2-quick-start)
3. [REST reference](#3-rest-reference)
   - 3.1 [System](#31-system) · 3.2 [Links](#32-links) · 3.3 [Jobs](#33-jobs) · 3.4 [Vehicles](#34-vehicles)
   - 3.5 [Parameters](#35-parameters) · 3.6 [MAVFTP](#36-mavftp) · 3.7 [Logs](#37-logs)
   - 3.8 [Missions, fences, rally points](#38-missions-fences-rally-points) · 3.9 [Terrain](#39-terrain) · 3.10 [Map tiles](#310-map-tiles)
   - 3.11 [Calibration](#311-calibration) · 3.12 [Manual control](#312-manual-control)
   - 3.13 [RTK / GPS injection](#313-rtk--gps-injection) · 3.14 [ADS-B](#314-ads-b) · 3.15 [Settings](#315-settings)
   - 3.16 [Spray-grid planning](#316-spray-grid-planning)
4. [WebSocket protocol](#4-websocket-protocol)
5. [Data model reference](#5-data-model-reference)
6. [Building a GCS UI on this API](#6-building-a-gcs-ui-on-this-api)
7. [Appendix: useful telemetry fields](#7-appendix-useful-telemetry-fields)

---

## 1. Conventions

### Base URL and hosts

| | Console server (`dotnet run --project Headless/MPBackend.Server`) | Tray app |
|---|---|---|
| Default URL | `http://127.0.0.1:5880` | `http://0.0.0.0:5880` (all interfaces) |
| REST | `/api/v1/...` | same |
| WebSocket | `/ws` | same |
| OpenAPI / Swagger UI | `/swagger/v1/swagger.json`, `/swagger` | same |
| Dev console | `/dev.html` | same |

There is **no authentication and no TLS**. CORS allows any origin, header and method, so a browser UI served
from another origin (a Vite dev server, an Electron `file://` page, a phone on the LAN) can call the API
directly. Only expose the backend on networks you trust.

### Vehicles are addressed by MAVLink ids, not by link

Every vehicle endpoint is `/api/v1/vehicles/{sysid}/{compid}/...`. The backend resolves the id pair to
whichever link currently carries that vehicle, so a UI never needs to know on which serial port or TCP
connection a vehicle arrived. Several vehicles on one link (a telemetry radio relaying a swarm) and one vehicle
reachable through several links both work. `compid` is normally `1` (the autopilot); other components
(gimbals, companion computers, peripherals) appear with their own component id when they send heartbeats.

### JSON

- Property names are **camelCase** (`sysId`, `connectedAt`, `tlogPath`). Note that the WebSocket telemetry and
  mavlink envelopes use the lower-case keys `sysid` / `compid` (see section 4); REST models use `sysId` /
  `compId`.
- Enums are **strings** (`"Connected"`, `"Tcp"`, `"GLOBAL_RELATIVE_ALT"`, `"MAV_SEVERITY_INFO"`), never numbers.
  Where the API accepts an enum, both the name and the number are accepted on input.
- `NaN`, `Infinity` and `-Infinity` are serialised as the **strings** `"NaN"`, `"Infinity"`, `"-Infinity"`
  (System.Text.Json named floating-point literals), because Mission Planner's `CurrentState` uses them for "no
  data yet". The output stays valid JSON, but a numeric telemetry field can therefore arrive as a string; treat
  a non-number in a numeric field as "no data". The same strings are accepted on input, and numbers sent to
  the backend may be given as strings in general (`"115200"`).
- Public *fields* of structs are serialised as well as properties (needed for raw MAVLink messages).
- Timestamps are ISO 8601 UTC with a `Z` suffix (`"2026-11-11T17:48:08.888234Z"`).
- Request bodies are `application/json` unless an endpoint states otherwise (file uploads accept `text/plain`
  raw bodies or `multipart/form-data` with a `file` part; MAVFTP uploads are `application/octet-stream`).

### Errors

Failures are RFC 7807 problem documents (`application/problem+json`) with the exception message in `detail`:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Not Found", "status": 404,
  "detail": "vehicle 1/1 not connected" }
```

| Status | When |
|---|---|
| 400 | Malformed input: unknown connection string, unknown MAV_CMD or MAVLink message name, bad mission type/format, invalid body |
| 404 | Unknown vehicle (`sysid/compid` not connected), link, job, setting, parameter name, FTP path, log file, map tile |
| 409 | Conflicting state: a calibration already running on that vehicle, RTK already started, a vehicle without MAVFTP, operation refused by the vehicle's current state |
| 499 | The client closed the request while the backend was still talking to the vehicle |
| 502 | The vehicle or the transport failed: MAVFTP error, timeout waiting for the vehicle, I/O error on the link |
| 500 | Anything else; the message is in `detail` and in `logs/mpbackend.log` |

Command-style endpoints (`mode`, `arm`, `command`, `takeoff`, `goto`, `reboot`, `missions/current`) do **not**
turn a vehicle *rejection* into an HTTP error: they return 200 with `{ "accepted": false, "message": ... }`
(the MAV_RESULT) so the UI can show the vehicle's reason. HTTP errors on those endpoints mean the command
could not be delivered at all.

### Jobs: long operations

Anything that takes more than a round trip to the vehicle (connecting, downloading parameters, writing many
parameters, downloading logs, prefetching tiles, refreshing parameter metadata) is a **job**. The endpoint
returns `202 Accepted` with a `Job` body immediately; progress arrives on the `jobs` WebSocket topic and can be
polled with `GET /api/v1/jobs/{id}`. `DELETE /api/v1/jobs/{id}` cancels a running job. Finished jobs are kept for
30 minutes.

```json
{ "id": "52bc33d1237c", "kind": "connect", "title": "Connect tcp:127.0.0.1:5760", "linkId": 1,
  "state": "Running", "progress": -1, "message": "Waiting for heartbeat",
  "error": null, "result": null, "startedAt": "2026-11-11T17:48:05.654Z", "finishedAt": null }
```

`state` is `Running | Succeeded | Failed | Cancelled`; `progress` is 0–100 or `-1` when indeterminate;
`result` is job-specific (see each endpoint). Job kinds: `connect`, `params.download`, `params.write`,
`params.metadata`, `logs.download`, `tiles.prefetch`.

Mission upload/download, parameter single set and calibration steps are *synchronous* requests instead (they
block until the vehicle answered, typically well under a second), but they also appear on the `jobs` topic
while running so a UI can show a busy indicator for everything in one place.

### Units

Raw MAVLink / ArduPilot units unless stated: degrees for angles and lat/lng, metres for distances and altitudes,
m/s for speeds, volts/amps for battery, `cm/s` only where the ADS-B section says so. Altitudes in `MissionItem`
follow the item's `frame` (relative to home for `GLOBAL_RELATIVE_ALT`, AMSL for `GLOBAL`, above terrain for
`GLOBAL_TERRAIN_ALT`). Telemetry fields keep Mission Planner's units (see the appendix).

---

## 2. Quick start

A minimal session with `curl`, the same calls a UI makes on its connect screen.

```bash
# 1. what is on this machine
curl http://127.0.0.1:5880/api/v1/info
curl http://127.0.0.1:5880/api/v1/serialports

# 2. open a link (SITL here; "COM5:57600" for a radio, "udp:14550" to listen)
curl -X POST http://127.0.0.1:5880/api/v1/links -H "content-type: application/json" \
     -d '{"connection":"tcp:127.0.0.1:5760"}'
# -> 202 { "link": { "id": 1, "state": "Connecting", ... }, "job": { "id": "52bc33d1237c", "kind": "connect", ... } }

# 3. wait for the connect job, then the parameter download job that follows it
curl http://127.0.0.1:5880/api/v1/jobs/52bc33d1237c          # until state != Running
curl http://127.0.0.1:5880/api/v1/vehicles                    # [{ "sysId": 1, "compId": 1, "mode": "Stabilize", ... }]

# 4. read telemetry once, then stream it over the WebSocket (section 4)
curl "http://127.0.0.1:5880/api/v1/vehicles/1/1/state?fields=roll,pitch,yaw,alt,lat,lng,groundspeed,mode,armed"

# 5. fly
curl -X POST http://127.0.0.1:5880/api/v1/vehicles/1/1/mode    -H "content-type: application/json" -d '{"mode":"Guided"}'
curl -X POST http://127.0.0.1:5880/api/v1/vehicles/1/1/arm     -H "content-type: application/json" -d '{"arm":true}'
curl -X POST http://127.0.0.1:5880/api/v1/vehicles/1/1/takeoff -H "content-type: application/json" -d '{"altitude":20}'
curl -X POST http://127.0.0.1:5880/api/v1/vehicles/1/1/goto    -H "content-type: application/json" \
     -d '{"lat":-35.3630,"lng":149.1660,"alt":30}'
curl -X POST http://127.0.0.1:5880/api/v1/vehicles/1/1/command -H "content-type: application/json" \
     -d '{"command":"MAV_CMD_NAV_RETURN_TO_LAUNCH"}'

# 6. close the link
curl -X DELETE http://127.0.0.1:5880/api/v1/links/1
```

The same with the TypeScript client:

```ts
import { createBackendClient, BackendSocket, waitForJob } from "@mpbackend/client";

const api = createBackendClient("http://127.0.0.1:5880");
const { data } = await api.POST("/api/v1/links", { body: { connection: "tcp:127.0.0.1:5760" } });
await waitForJob(api, data!.job.id);

const ws = new BackendSocket("http://127.0.0.1:5880");       // reconnects and re-subscribes by itself
ws.on("links", e => console.log(e.event, e.link.state));
ws.on("status", s => console.log(s.severity, s.text));
ws.telemetry(1, 1, (state, changed) => hud.update(state), { fields: ["roll", "pitch", "alt"], rate: 20 });

await api.POST("/api/v1/vehicles/{sysid}/{compid}/mode", { params: { path: { sysid: 1, compid: 1 } }, body: { mode: "Guided" } });
```

---

## 3. REST reference

All paths below are relative to `/api/v1`. "Vehicle" endpoints are under `/vehicles/{sysid}/{compid}`.

### 3.1 System

| Method & path | Returns |
|---|---|
| `GET /info` | `{ version, dataDirectory, logDirectory, topics[] }`: backend version, where it keeps its files, and the WebSocket topic names it supports. Call it first: it doubles as a reachability check. |
| `GET /serialports` | `[{ name: "COM5", description: "USB Serial Device (COM5)" }]`: serial ports present on the backend's machine (the host running the backend, not the browser's machine). |
| `GET /telemetry/fields` | `[{ name, type, displayText, group }]`: every `CurrentState` field readable through `/state` and the `telemetry` topic (about 1000). `type` is `number`, `integer`, `bool`, `string`, `enum`, `datetime` or `timespan`; `displayText` and `group` are Mission Planner's labels for the quick-view / status tabs (null when MP has none). Use it to build a field picker; cache it, it never changes at runtime. |
| `POST /params/metadata/update?force=` | Job (`params.metadata`): re-download `apm.pdef.xml` parameter documentation for all vehicle types. Cached for 7 days unless `force=true`. Result `{ version }`. |

### 3.2 Links

A link is one transport the backend reads MAVLink from. Vehicles are discovered from heartbeats on it.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET /links` | | `LinkInfo[]` |
| `GET /links/{id}` | | `LinkInfo`, 404 |
| `POST /links` | `ConnectDto` | 202 `{ link: LinkInfo, job: Job }` (`Location: /api/v1/links/{id}`) |
| `DELETE /links/{id}` | | 204 after the link is closed; 404 |

`ConnectDto` is either a compact connection string or explicit fields:

```json
{ "connection": "tcp:127.0.0.1:5760" }
{ "connection": "COM5:57600" }
{ "type": "Serial", "port": "COM5", "baud": 57600, "fetchParams": true }
```

| Connection string | Type | Meaning |
|---|---|---|
| `tcp:host:port` | `Tcp` | TCP client (SITL, ser2net, WiFi telemetry bridges) |
| `udp:port` | `Udp` | UDP server: listen on a local port, the vehicle/MAVProxy sends to us (`udp:14550`) |
| `udpcl:host:port` | `UdpClient` | UDP client: we send to host:port first |
| `COM5:57600`, `serial:COM5:57600`, `/dev/ttyUSB0:57600` | `Serial` | Serial port; baud defaults to 115200 |
| `ws:ws://host/path`, `ws://host/path` | `WebSocket` | WebSocket transport |

`fetchParams` (default `true`) downloads the full parameter list once the vehicle is identified; set it to
`false` for peripherals or when a UI only needs telemetry (the UI can start the download later with
`POST .../params/refresh`).

**Connect flow**: the returned link is `Connecting`; the `connect` job reports `"Opening ..."` then
`"Waiting for heartbeat"`, and succeeds once the first vehicle has sent a heartbeat and answered the version
handshake (`result`: the vehicle text). The link becomes `Connected`, a `vehicles` event `detected` is published
for each vehicle, and (with `fetchParams`) a `params.download` job starts automatically. A link that never sees
a heartbeat (wrong port, vehicle off) ends `Failed` with `error` set; delete it and try again.

`LinkInfo`:

```json
{ "id": 1, "connection": "tcp:127.0.0.1:5760", "type": "Tcp", "state": "Connected", "error": null,
  "connectedAt": "2026-11-11T17:48:08.888Z",
  "tlogPath": "C:\\Users\\me\\AppData\\Local\\MPBackend\\Mission Planner\\logs\\2026-11-11 18-48-05-link1.tlog",
  "vehicles": [ { "sysId": 1, "compId": 1 } ] }
```

`state` is `Connecting | Connected | Disconnected | Failed`. `Disconnected` means the transport dropped after a
successful connection (serial unplugged, TCP reset); the link stays in the list with `error` set until it is
deleted, so a UI can show "lost" and offer reconnect. The backend records a `.tlog` per link (unless
`RecordTlogs` is off) whose path is `tlogPath`.

### 3.3 Jobs

| Method & path | Returns |
|---|---|
| `GET /jobs` | `Job[]`, newest first (running and finished within the last 30 minutes) |
| `GET /jobs/{id}` | `Job`, 404 |
| `DELETE /jobs/{id}` | 202 when a running job was asked to cancel (it reaches `Cancelled` shortly after), 404 when unknown or already finished |

Prefer the `jobs` WebSocket topic over polling: every progress change is published (throttled to 10 Hz per job,
always including the final state).

### 3.4 Vehicles

| Method & path | Body | Returns |
|---|---|---|
| `GET /vehicles` | | `VehicleInfo[]` for every vehicle on a `Connected` link |
| `GET /vehicles/{s}/{c}` | | `VehicleInfo`, 404 |
| `GET /vehicles/{s}/{c}/state?fields=a,b,c` | | `{ fieldName: value, ... }`: snapshot of `CurrentState`. `fields` is a comma-separated list of names from `/telemetry/fields` (case-insensitive); all ~1000 fields when omitted (about 40 KB, fine for a one-off, not for polling). |
| `GET /vehicles/{s}/{c}/messages` | | `[{ time, text }]`: STATUSTEXT history kept by MP for this vehicle (newest last) |
| `GET /vehicles/{s}/{c}/modes` | | `[{ number, name }]`: the flight modes valid for this firmware (Copter, Plane, Rover, Sub, Tracker...). Build mode menus from this; never hard-code. |
| `POST /vehicles/{s}/{c}/mode` | `{ "mode": "Guided" }` (name as in `/modes`, case-insensitive) | `CommandResult` |
| `POST /vehicles/{s}/{c}/arm` | `{ "arm": true, "force": false }` | `CommandResult`. `force: true` passes the ArduPilot force-arm magic (bypasses arming checks); expose it only behind a deliberate UI. |
| `POST /vehicles/{s}/{c}/takeoff` | `{ "altitude": 20 }` metres above home | `CommandResult` (NAV_TAKEOFF; the vehicle must be armed and in Guided for Copter) |
| `POST /vehicles/{s}/{c}/goto` | `{ "lat", "lng", "alt" }` alt relative to home | `CommandResult`: Mission Planner's guided-mode target (`setGuidedModeWP`, the same packet the "Fly to here" map action sends) |
| `POST /vehicles/{s}/{c}/reboot` | `{ "bootloader": false }` | `CommandResult`; `bootloader: true` reboots into the bootloader (for flashing). Sent without waiting for an ack (the vehicle goes away), so `accepted` is always `true`; the link stays open and the vehicle is re-detected when it comes back. |
| `POST /vehicles/{s}/{c}/command` | `CommandDto` | `CommandResult` |

`CommandResult` is `{ "accepted": true|false, "message": "..." }` where `message` carries the MAV_RESULT name or
the vehicle's rejection text when not accepted.

`CommandDto` sends any MAV_CMD:

```json
{ "command": "MAV_CMD_DO_SET_SERVO", "params": [9, 1500] }
{ "command": "DO_CHANGE_SPEED", "params": [1, 12, -1, 0] }
{ "command": 20 }                                             // RETURN_TO_LAUNCH by number
{ "command": "MAV_CMD_DO_REPOSITION", "useInt": true, "frame": "GLOBAL_RELATIVE_ALT",
  "params": [-1, 1, 0, "NaN", -353630000, 1491660000, 30] } // COMMAND_INT: params 5/6 are degE7 ("NaN" = leave yaw)
```

- `command`: the spec name (`MAV_CMD_NAV_TAKEOFF`), Mission Planner's short form (`TAKEOFF`, `NAV_TAKEOFF`) or the
  number. Unknown names give 400.
- `params`: up to 7 floats, missing ones are 0. With `useInt: true` a COMMAND_INT is sent and params 5/6 are
  lat/lon as integers in 1e-7 degrees; `frame` (default `GLOBAL_RELATIVE_ALT`) is only used by COMMAND_INT.
- `requireAck` (default `true`): wait for COMMAND_ACK and report it. With `false` the call returns as soon as the
  packet is sent with `accepted: true`.

`VehicleInfo`:

```json
{ "linkId": 1, "sysId": 1, "compId": 1, "type": "QUADROTOR", "autopilot": "ARDUPILOTMEGA",
  "firmware": "ArduCopter2", "version": "ArduCopter V4.8.0-dev (8e890ec0)", "mode": "Stabilize", "armed": false,
  "linkQuality": 100, "lastPacket": "2026-11-11T17:48:25.687Z", "paramsReceived": 1434, "paramsTotal": 1434 }
```

`type` is the MAV_TYPE name, `firmware` MP's firmware class (`ArduCopter2`, `ArduPlane`, `ArduRover`, `ArduSub`,
`ArduTracker`, `PX4`...), `linkQuality` 0–100 % from heartbeat/packet loss, `paramsReceived/Total` the parameter
download state (`paramsTotal` 0 until the vehicle reported its count).

### 3.5 Parameters

Vehicle parameters are cached in the backend once downloaded (local cache, then MAVFTP `@PARAM/param.pck` which
also yields the defaults, then PARAM_REQUEST_LIST). All endpoints are per vehicle unless marked global.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET .../params?metadata=true` | | `ParamValue[]`, or `ParamWithMetadata[]` (`ParamValue` + `metadata: ParamMetadata|null`) with `metadata=true`. Thousands of entries; load once after the `params` topic reports `loaded`. |
| `GET .../params/{name}` | | `ParamValue`, 404 |
| `PUT .../params/{name}` | `{ "value": 15, "force": false }` | `ParamSetResult` (synchronous; waits for the vehicle's PARAM_VALUE echo) |
| `POST .../params/write` | `{ "values": { "WPNAV_SPEED": 1000, "RTL_ALT": 3000 } }` | 202 Job (`params.write`); result `{ written, failed: ParamSetResult[], rebootRequired: string[] }` |
| `POST .../params/refresh?cache=` | | 202 Job (`params.download`): re-download the list from the vehicle; `cache=true` allows the local cache if younger than `ParamCacheMaxAge`. Result `{ count, source }` (`source` = `cache`, `ftp` or `mavlink`). |
| `GET .../params/metadata?names=A,B` | | `{ name: ParamMetadata }` for the given names (all known when omitted) |
| `GET .../params/file` | | `text/plain` attachment: the parameters as a Mission Planner `.param` file (`NAME,VALUE` lines, header with the vehicle version) |
| `POST .../params/compare` | `.param`/`.parm`/QGC text, raw body or multipart `file` | `ParamDiff[]`: `{ name, vehicleValue (null when the vehicle lacks it), fileValue }` for every differing entry |
| `POST .../params/parse` | same | `{ name: value }` parsed from the file (no vehicle needed, the route just lives under a vehicle) |

```json
// ParamValue
{ "name": "WPNAV_SPEED", "value": 1000, "type": "REAL32", "default": 1000 }
// ParamSetResult
{ "name": "WPNAV_SPEED", "accepted": true, "value": 1200, "error": null, "rebootRequired": false }
// ParamMetadata (from apm.pdef.xml; values/bitmask are null when not applicable)
{ "name": "FLTMODE1", "displayName": "Flight Mode 1", "description": "Flight mode when pwm of Flightmode channel(FLTMODE_CH) is <= 1230",
  "units": null, "min": null, "max": null, "increment": null,
  "values": [ { "value": 0, "name": "Stabilize" }, { "value": 2, "name": "AltHold" } ], "bitmask": null,
  "user": "Standard", "rebootRequired": false, "readOnly": false }
```

- `type` is the MAV_PARAM_TYPE name (`INT8`, `INT16`, `INT32`, `REAL32`...); `default` is null when the vehicle
  did not provide defaults.
- `force: true` on PUT skips the client-side range check for parameters with `min`/`max` metadata.
- `rebootRequired` on a set result mirrors the metadata flag; after writing such parameters offer a reboot.
- Volatile parameters (`STAT_*`, `SYSID_*`...) are skipped when applying files, as Mission Planner does.
- `params` topic events: `loaded` (list complete, `count`), `changed` (`changed[]` lists the new values; sent
  for every accepted set/write and for PARAM_VALUE updates initiated by the vehicle).

### 3.6 MAVFTP

Direct access to the autopilot's file system (SD card, `@ROMFS`, `@SYS`, `@PARAM`). Paths are absolute vehicle
paths such as `/APM/LOGS` or `@SYS/threads.txt`. Transfers use a per-link lock, so a long FTP download does
not block parameter writes or commands, but two FTP transfers to the same vehicle are serialised.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET .../ftp/list?path=/APM` | | `[{ name, path, isDirectory, size, modifiedUtc }]` (`modifiedUtc` null when the vehicle does not report it) |
| `GET .../ftp/file?path=/APM/LOGS/00000012.BIN` | | `application/octet-stream` attachment |
| `PUT .../ftp/file?path=/APM/scripts/x.lua` | raw `application/octet-stream` body | 204 |
| `DELETE .../ftp/file?path=` | | 204 |
| `DELETE .../ftp/dir?path=&recursive=true` | | 204 (`recursive` deletes contents first) |
| `POST .../ftp/mkdir` | `{ "path": "/APM/scripts" }` | 204 |
| `POST .../ftp/rename` | `{ "from": "/APM/a.txt", "to": "/APM/b.txt" }` | 204 |
| `GET .../ftp/crc?path=` | | `{ path, crc32 }` computed by the vehicle (verify uploads) |

FTP errors from the vehicle surface as 502 with the MAVFTP error text; 409 when the vehicle has no FTP service.
Downloads of large logs via FTP are slow (tens of KB/s on a radio): use the log download job for dataflash logs.

### 3.7 Logs

| Method & path | Body | Returns |
|---|---|---|
| `GET .../logs` | | `[{ id, size, timeUtc }]`: onboard dataflash logs (LOG_ENTRY) |
| `POST .../logs/download` | `{ "ids": [12, 13], "allowArmed": false }` | 202 Job (`logs.download`); progress by bytes; result `{ files: [ "bin/1/2026-11-11 10-21-33.bin" ] }` (paths relative to the backend log directory). Refused with 409 while armed unless `allowArmed`. Cancelling takes effect between logs. |
| `DELETE .../logs` | | 204: erase all onboard logs |
| `GET /logs/local` (global) | | `[{ path, kind, size, modifiedUtc }]`: files in the backend's log directory; `kind` is `bin` (downloaded dataflash) or `tlog`/`rlog` (recorded telemetry) |
| `GET /logs/local/file?path=` (global) | | the file as an attachment; supports HTTP range requests (resumable, seekable playback). Paths outside the log directory are refused. |

Dataflash log *analysis* (graphs, parsing) is not part of the backend; a UI downloads the `.bin` and uses its
own tooling.

### 3.8 Missions, fences, rally points

`{type}` is `mission`, `fence` or `rally`. The backend speaks the MAVLink mission protocol for each type and
converts between ArduPilot's "item 0 is home" convention and a plan with an explicit `home`.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET .../missions/{type}` | | `MissionPlan` downloaded from the vehicle |
| `GET .../missions/{type}?format=waypoints\|plan\|kml` | | the same as a file attachment (`.waypoints` QGC WPL 110 text, `.plan` QGC JSON, `.kml`) |
| `PUT .../missions/{type}` | `MissionPlan` | `{ uploaded: n }` items written (excluding home) |
| `POST .../missions/{type}/file?filename=x.plan` | `.waypoints`/`.plan`/`.kml` text, raw or multipart `file` | `{ uploaded }`: parse and upload the list of that type from the file (400 when the file has none). `filename` chooses the parser when the body is raw; multipart uses the part's file name. |
| `DELETE .../missions/{type}` | | 204: clear the list (missions keep home) |
| `POST .../missions/current` | `{ "seq": 3 }` | `{ accepted }`: MISSION_SET_CURRENT, jump to item `seq` (1-based as the vehicle counts, home is 0) |
| `POST /missions/parse?filename=` (global) | file text | `{ format, mission, fence, rally }` with nulls for lists the file lacks |
| `POST /missions/export?format=` (global) | `{ mission?, fence?, rally? }` of `MissionPlan` | the file as an attachment |

`MissionPlan` / `MissionItem`:

```json
{ "type": "MISSION",
  "home": { "lat": -35.363261, "lng": 149.165230, "alt": 584.0 },
  "items": [
    { "command": 22, "commandName": "TAKEOFF", "frame": "GLOBAL_RELATIVE_ALT",
      "param1": 0, "param2": 0, "param3": 0, "param4": 0, "lat": 0, "lng": 0, "alt": 30 },
    { "command": 16, "commandName": "WAYPOINT", "frame": "GLOBAL_RELATIVE_ALT",
      "param1": 0, "param2": 0, "param3": 0, "param4": 0, "lat": -35.3620, "lng": 149.1650, "alt": 50 },
    { "command": 20, "commandName": "RETURN_TO_LAUNCH", "frame": "GLOBAL_RELATIVE_ALT",
      "param1": 0, "param2": 0, "param3": 0, "param4": 0, "lat": 0, "lng": 0, "alt": 0 } ] }
```

- On upload either `command` (MAV_CMD number) or `commandName` (any of `WAYPOINT`, `NAV_WAYPOINT`,
  `MAV_CMD_NAV_WAYPOINT`) identifies the command; `command` wins when non-zero. `frame` defaults to
  `GLOBAL_RELATIVE_ALT`. `home.alt` is metres AMSL.
- Mission upload: `home` is optional; when absent the vehicle's current home is sent as item 0. After the upload
  the backend re-reads home and requests terrain for every point so terrain-frame items work.
- Fence items are always sent in the `GLOBAL` frame; `FENCE_POLYGON_VERTEX_INCLUSION` and friends carry the vertex
  count in `param1` exactly as in the mission protocol. Rally items are `RALLY_POINT` commands.
- `GET` returns ArduPilot's item 0 as `home`, so `items[0]` is the first real command (`seq` 1 for
  `missions/current`).
- `missions` topic events: `downloaded` / `uploaded` with `type` and `count`.

### 3.9 Terrain

Heights come from Mission Planner's SRTM support (GeoTIFF/DTED files in the `srtm` folder first, then SRTM
tiles downloaded on demand). The backend also answers the vehicle's TERRAIN_REQUEST automatically.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET /terrain/elevation?lat=&lng=&wait=5` | | `{ lat, lng, alt, status, source, geoidUndulation }`; `status` is `Valid`, `Ocean` (alt 0) or `Unavailable` (alt null: tile still downloading or no coverage). `wait` (seconds, default 5) is how long to wait for a download before answering `Unavailable`. |
| `POST /terrain/profile` | `{ "path": [ { "lat", "lng" }, ... ], "spacing": 30, "wait": 10 }` | `[{ distance, lat, lng, alt }]` sampled every `spacing` metres along the polyline (`distance` from the start); `alt` null where unavailable. Use it for the mission altitude profile view. |
| `GET /terrain/geoid?lat=&lng=` | | `{ lat, lng, undulation }`: EGM96 geoid height above the WGS84 ellipsoid, for converting GPS ellipsoid heights to AMSL |

### 3.10 Map tiles

The backend proxies and caches GMap.NET's tile providers so a UI can draw maps without its own API keys and
works offline from the cache.

| Method & path | Body / query | Returns |
|---|---|---|
| `GET /maps/providers` | | `[{ name, id, minZoom, maxZoom }]` e.g. `GoogleSatelliteMap`, `BingHybridMap`, `OpenStreetMap`, `ArcGIS_World_Imagery`... |
| `GET /maps/tiles/{provider}/{z}/{x}/{y}` | | the tile image (`image/png` or `image/jpeg`, `Cache-Control: public, max-age=86400`), 404 when the provider has no tile there |
| `POST /maps/prefetch` | `{ "provider", "north", "west", "south", "east", "minZoom", "maxZoom" }` | 202 Job (`tiles.prefetch`); result `{ tiles, failed }` |
| `GET /maps/mode` / `PUT /maps/mode` | `{ "mode": "ServerAndCache" }` | `ServerAndCache` (default), `CacheOnly` (offline: never fetch), `ServerOnly` |

Tiles are standard XYZ (Web Mercator), so any map library works unchanged, e.g. Leaflet:

```js
L.tileLayer("http://127.0.0.1:5880/api/v1/maps/tiles/GoogleSatelliteMap/{z}/{x}/{y}", { maxZoom: 20 }).addTo(map);
```

### 3.11 Calibration

Ports of Mission Planner's setup screens, driven through state and events instead of dialogs. **One calibration
per vehicle at a time** (409 otherwise). Progress and prompts are published on the `calibration` topic as a
`CalibrationStatus`; `GET .../calibration` returns the current or last one (204 when none ran).

| Method & path | Body | Flow |
|---|---|---|
| `POST .../calibration/accel/start` | | 6-position accelerometer calibration. The status goes `WaitingForUser` with `position` = `LEVEL`, `LEFT`, `RIGHT`, `NOSEDOWN`, `NOSEUP`, `BACK`; show it, let the user place the vehicle, then call `accel/next`. Ends `Succeeded`/`Failed` with `message`. Returns when the whole flow finished. |
| `POST .../calibration/accel/next` | | confirm the current position |
| `POST .../calibration/preflight/{type}` | | one-shot MAV_CMD_PREFLIGHT_CALIBRATION: `level` (accel trim), `simpleaccel`, `gyro`, `baro`, `airspeed`. Returns a `CommandResult` (the vehicle's ack), not a `CalibrationStatus`, and does not appear on the `calibration` topic; keep the vehicle still while it runs. |
| `POST .../calibration/compass/start` | `{ "mask": 0, "retry": true, "autosave": true, "delay": 0, "autoreboot": false }` (all optional) | onboard compass calibration: the user rotates the vehicle; `compasses[]` carries per-compass `percent`/`status`/`attempt`, `reports[]` the fitness and offsets at the end. `mask` 0 = all compasses. |
| `POST .../calibration/compass/accept` | | accept the results (needed when `autosave` is false); reboot the vehicle afterwards |
| `POST .../calibration/compass/fixed-yaw` | `{ "yaw": 90 }` degrees true | large-vehicle calibration from a known heading (needs GPS lock) |
| `POST .../calibration/radio/start` | | start RC calibration: `channels[]` (`channel`, `value`, `min`, `max`) streams on the topic as the user moves sticks and switches to their extremes |
| `POST .../calibration/radio/finish` | | with sticks centred and throttle down: writes `RCx_MIN/MAX/TRIM` and returns the status |
| `POST .../calibration/cancel` | | abort whatever is running |

```json
// CalibrationStatus
{ "kind": "Accel", "sysId": 1, "compId": 1, "state": "WaitingForUser",
  "message": "Place vehicle LEFT side down", "position": "LEFT",
  "compasses": null, "reports": null, "channels": null, "updated": "2026-11-11T18:02:11.1Z" }
```

`kind` is `Accel | Compass | Radio`; `state` is `Running | WaitingForUser | Succeeded | Failed | Cancelled`.

### 3.12 Manual control

Stick input from a browser gamepad or on-screen controls. The backend repeats the latest frame to the vehicle
at 20 Hz as RC_CHANNELS_OVERRIDE (or MANUAL_CONTROL) and **releases all overrides 500 ms after the last frame**,
so a closed tab or a lost WebSocket hands control back to the RC receiver.

| Method & path | Body | Returns |
|---|---|---|
| `POST .../manual-control` | `ManualControlInput` | `ManualControlStatus` |
| `GET .../manual-control` | | `ManualControlStatus`, 204 when never used |
| `DELETE .../manual-control` | | 204: stop and release immediately |

For continuous input use the WebSocket `manual_control` op (same `ManualControlInput` fields, no reply);
the REST endpoint is for occasional frames and tests. `ManualControlInput` uses exactly one of:

```json
{ "normalized": { "1": 0.0, "2": 0.0, "3": -1.0, "4": 0.0 } }   // channel -> -1..1 mapped with RCx_MIN/TRIM/MAX
{ "channels": [1500, 1500, 1000, 1500, null, null, 0, null] }   // raw PWM per channel (index 0 = CH1); null = untouched, 0 = release
{ "manual": { "x": 0, "y": 0, "z": 500, "r": 0, "buttons": 0 } } // MANUAL_CONTROL axes -1000..1000 (z 0..1000)
```

The throttle channel (`RCMAP_THROTTLE`) maps `-1..1` linearly from min to max; other channels map through trim.
`ManualControlStatus`: `{ sysId, compId, active, mode ("rc_override" | "manual_control"), lastInput, channels[] }`.

### 3.13 RTK / GPS injection

Reads RTCM3 corrections from a source and injects them into **every connected vehicle** (GPS_RTCM_DATA).

| Method & path | Body | Returns |
|---|---|---|
| `GET /rtk` | | `RtkStatus` |
| `POST /rtk/start` | `RtkStartRequest` | `RtkStatus` (409 when already running) |
| `POST /rtk/stop` | | `RtkStatus` |
| `POST /rtk/ublox/survey-in` | `{ "duration": 60, "accuracy": 2.0, "m8p130Plus": false }` | `RtkStatus`: configure a u-blox base on the serial source for survey-in |
| `POST /rtk/ublox/fixed` | `{ "lat", "lng", "alt", "m8p130Plus" }` | `RtkStatus`: configure a u-blox base with a known position |

```json
// RtkStartRequest: type Ntrip | Serial | Tcp | Udp | UdpClient
{ "type": "Ntrip", "url": "ntrip://user:pass@caster.example.com:2101/MOUNT", "ntripV1": false }
{ "type": "Serial", "port": "COM7", "baud": 115200 }
{ "type": "Tcp", "host": "192.168.1.20", "port": "2101" }
// RtkStatus
{ "running": true, "source": "ntrip://caster.example.com:2101/MOUNT", "error": null,
  "bytesReceived": 183220, "bpsIn": 980, "bpsSent": 980, "injected": 2210,
  "messages": { "1005": 12, "1077": 120, "1087": 120 },
  "lastSeen": { "gps": 0.4, "glonass": 0.4, "galileo": 1.2, "beidou": 9.9, "base": 4.1 },
  "basePosition": { "lat": 47.49, "lng": 19.04, "alt": 151.2 },
  "surveyIn": { "valid": false, "active": false, "duration": 0, "observations": 0, "meanAccuracy": 0 } }
```

Credentials in `url` are never echoed back (`source` strips them). The NTRIP GGA sentence follows the active
vehicle's position. Status updates are published on the `rtk` topic about once per second while running.

### 3.14 ADS-B

| Method & path | Body | Returns |
|---|---|---|
| `GET /adsb/aircraft` | | `Aircraft[]` nearest first: `{ icao, callSign, lat, lng, alt (m), heading (deg), speed (cm/s), verticalSpeed, squawk, threatLevel, source ("vehicle" \| "external"), lastSeen, distance (m from the active vehicle, null without position) }` |
| `GET /adsb/config` | | `AdsbConfig` |
| `PUT /adsb/config` | `{ "enabled": true, "server": "192.168.1.5", "port": 30003, "forwardToVehicle": true }` | `AdsbConfig` |

`server` is a dump1090-style host (SBS on 30003, AVR on 30002, Beast) or an `http(s)://` base URL of an
ADSBExchange-v2-compatible API (`https://api.adsb.lol`) queried around the vehicle. Vehicle-reported
ADSB_VEHICLE traffic is always included. Aircraft expire 30 s after the last report; the list is published on
the `adsb` topic whenever it changes.

### 3.15 Settings

Mission Planner's key/value store (`config.xml` in the data directory). A UI may keep its own preferences
here under its own key prefix so they travel with the backend's data directory.

| Method & path | Body | Returns |
|---|---|---|
| `GET /settings` | | `{ key: value }` of every key; values of keys matching `pass`, `secret`, `apikey`, `api_key`, `token` are masked as `***` |
| `GET /settings/{key}` | | `{ value }`, 404 when missing or secret |
| `PUT /settings/{key}` | `{ "value": "..." }` | 204 (saved to disk immediately) |
| `DELETE /settings/{key}` | | 204, 404 |

### 3.16 Spray-grid planning

The SprayGrid-Soleon Mission Planner plugin as an endpoint: parallel spray strips across a field polygon, cut
around obstacles, turned into an ArduPilot mission with the sprayer's Lua script messages. No vehicle is
needed; the result is a `MissionPlan` for `PUT .../missions/mission` or a file.

| Method & path | Body / query | Returns |
|---|---|---|
| `POST /planning/spraygrid/preview` | `SprayGridRequest` | `{ points, stats, warnings }`: the strips as S/E points with the requested altitude, plus area / strips / distances / times. Fast; call it on every parameter change. |
| `POST /planning/spraygrid/mission` | `SprayGridRequest`, optional `?format=waypoints\|plan\|kml` | `{ mission, points, stats, segmentStartSeq, warnings }` with the full `MissionPlan` (home + items), or the mission file as an attachment when `format` is given. Waits for terrain (see below). |

Request errors (polygon with fewer than 3 vertices, no home, non-positive `lineDistance` / `flySpeed`, a
field too small for any strip on `/mission`) are 400 with the reason in the body.

`SprayGridRequest` (metres, m/s, degrees; everything but `polygon` and `home` has a default):

```json
{ "polygon": [ { "lat": -35.3632, "lng": 149.1652 }, ... ],
  "obstacles": [ [ { "lat": ..., "lng": ... }, ... ] ],
  "home": { "lat": -35.3634, "lng": 149.1650, "alt": null },
  "lineDistance": 5, "angle": 0, "shift": 0, "laneSeparation": 0,
  "startFrom": "Home", "startPoint": null, "extendedEndpoint": true,
  "litersPerHa": 20, "sprayBarWidth": 4, "flySpeed": 5, "expandObstacles": true, "ignoreObstacles": false,
  "altitude": 3, "gpsOffset": 0.5, "altitudeReference": "Relative",
  "altitudeTracking": false, "trackingAltitudeError": 0.5, "takeoffAltitude": 5,
  "addTakeoffAndRtl": true, "headLock": false, "delayAtWaypoint": 0,
  "splitBy": "None", "splitValue": 0, "wpnavSpeed": null,
  "elevationSource": "Backend", "elevation": null, "terrainWait": 10 }
```

- **Grid**: `lineDistance` between strips, `angle` is the strip bearing in the UTM grid (0 = grid north),
  `shift` moves the whole grid sideways, `laneSeparation` is the minimum number of strips skipped between
  consecutive strips (0 = adjacent), `startFrom` is `Home`, `BottomLeft`, `TopLeft`, `BottomRight`,
  `TopRight` or `Point` (then `startPoint` must be one of a previous preview's S/E points),
  `extendedEndpoint` picks the next strip from the end of the current one.
- **Spray**: `litersPerHa`, `sprayBarWidth` (also the vehicle footprint for terrain sampling), `flySpeed` on
  the strips, `expandObstacles` grows each obstacle by half the bar width, `ignoreObstacles` plans the whole
  field. Obstacle polygons must lie inside the field; circles are sent as polygons.
- **Altitude**: waypoints fly at `altitude + gpsOffset` in the `altitudeReference` frame (`Relative`,
  `Absolute`, `Terrain`). With an elevation source the Relative/Absolute altitudes are terrain-corrected per
  waypoint (terrain under the point + target − terrain under home, as Mission Planner's "verify height" did
  for the plugin). `altitudeTracking` additionally samples the highest terrain under the spray-bar footprint
  (0.2 m steps) and inserts extra waypoints along each strip wherever the straight leg would be more than
  `trackingAltitudeError` off the target height; inserted points have an empty `tag`.
- **Mission**: `addTakeoffAndRtl` wraps each segment in TAKEOFF (`takeoffAltitude`, relative) and
  RETURN_TO_LAUNCH; `headLock` adds CONDITION_YAW with the strip angle; `delayAtWaypoint` is WAYPOINT
  param1; `wpnavSpeed` (the vehicle's WPNAV_SPEED in m/s, read it from `GET .../params/WPNAV_SPEED` ÷ 100
  when a vehicle is connected) adds a DO_CHANGE_SPEED back to it after the last strip.
- **Splits**: `splitBy` `Time` (`splitValue` minutes per segment including the flight home, estimated at
  `flySpeed`) or `Segments` (`splitValue` segments cut at strip starts). Every segment is a full block
  (sprayer setup, takeoff, speed, strips, RTL) and the mission starts with one DO_JUMP per segment so the
  vehicle can be started at any segment with `POST .../missions/current` and `seq` from `segmentStartSeq`.
  `stats.splitTimesS` holds the estimated duration of each time segment; a warning says when the time is too
  short for the flight to the field and back.
- **Terrain**: `elevationSource` `Backend` uses the srtm store (waits up to `terrainWait` seconds for tiles
  to download; a warning is added when they were still missing, then retry), `Raster` uses `elevation`
  (`{ north, south, west, east, cols, rows, heights[] }`, row-major from the north-west corner, metres AMSL,
  null where unknown; the UI samples it from its own terrain provider), `None` applies no terrain correction.
  `home.alt` is sampled from the elevation source when null.

Sprayer protocol in the generated mission (unchanged from the plugin, consumed by the vehicle's Lua script):
`DO_SEND_SCRIPT_MESSAGE` (217) with `param1 = 1` and `param2..4 = litersPerHa, lineDistance, flySpeed` once
per segment, `param1 = 2, param2 = 3, param3 = distance to the strip end` after every strip-start waypoint and
`param1 = 2, param2 = 0` after every strip-end waypoint. Strip starts and ends carry `param4 = 1` / `2` on
the WAYPOINT; Relative waypoints carry the height above the terrain under the point in `param3`.

Differences from the plugin: altitudes are not truncated to whole metres; altitude-tracking points are kept in
split missions; the last segment of a `Segments` split always reaches the end of the grid; the plugin's
disabled obstacle-routing code (`I` points) is not ported; display-unit conversion is the UI's job.

---

## 4. WebSocket protocol

Connect to `ws://host:port/ws`. One socket per UI is enough: it multiplexes event topics, any number of
telemetry streams, a raw MAVLink stream and manual control. Frames are UTF-8 JSON text; the server sends
`{ "topic": "...", "data": ... }` envelopes. The server pings every 15 s at the protocol level; no application
keep-alive is required, but `{"op":"ping"}` is available.

On connect the server sends the topic list:

```json
{ "topic": "welcome", "data": { "topics": ["links","vehicles","status","jobs","params","missions","calibration","rtk","adsb","telemetry","mavlink"] } }
```

### Client → server operations

| Op | Fields | Effect |
|---|---|---|
| `subscribe` | `topic` | start receiving an event topic; reply `{"topic":"subscribed","data":{...}}` |
| `subscribe` | `topic: "telemetry"`, `sysid`, `compid`, `fields?: string[]`, `rate?: 1–50` | start (or replace) the telemetry stream for that vehicle |
| `subscribe` | `topic: "mavlink"`, `sysid?`, `compid?`, `msgs?: string[]` | start (or replace) the raw MAVLink stream with a filter |
| `unsubscribe` | `topic` (+ `sysid`, `compid` for telemetry; without them all telemetry streams stop) | stop |
| `manual_control` | `sysid`, `compid`, plus `normalized` / `channels` / `manual` | one stick frame (no reply) |
| `manual_control_stop` | `sysid`, `compid` | release overrides now |
| `ping` | | `{"topic":"pong","data":"<utc time>"}` |

Malformed or unknown requests get `{ "topic": "error", "data": "<message>" }` and the socket stays open.

### Event topics

Subscribing to an event topic delivers the same objects the REST API returns, pushed when something changes:

| Topic | `data` | When |
|---|---|---|
| `links` | `{ event: "added" \| "connected" \| "failed" \| "lost" \| "removed", link: LinkInfo }` | link life cycle: `added` (POST accepted, `Connecting`), `connected` (first heartbeat), `failed` (no heartbeat / open error), `lost` (transport dropped while connected: state `Disconnected`, `error` "Connection lost"), `removed` (DELETE) |
| `vehicles` | `{ event: "detected", linkId, sysId, compId }` | a new vehicle sent its first heartbeat on a link; fetch `GET /vehicles/{s}/{c}` for details |
| `status` | `{ linkId, sysId, compId, severity: "MAV_SEVERITY_INFO"..., text, time }` | every STATUSTEXT, as it arrives (this is the "messages" console of a GCS) |
| `jobs` | `Job` | job created, progress (≤10 Hz per job), finished |
| `params` | `{ event: "loaded" \| "changed", sysId, compId, count, changed: ParamValue[] \| null }` | parameter list complete; values changed |
| `missions` | `{ event: "downloaded" \| "uploaded", sysId, compId, type: "MISSION" \| "FENCE" \| "RALLY", count }` | after a transfer |
| `calibration` | `CalibrationStatus` | every step of a running calibration |
| `rtk` | `RtkStatus` | about 1 Hz while injecting, and on start/stop |
| `adsb` | `Aircraft[]` | whenever the traffic picture changes |

Events are not replayed: on (re)connect fetch the current state with the REST endpoints (`GET /links`,
`GET /vehicles`, `GET /jobs`) and then subscribe. Subscribe first and then fetch if you prefer; duplicates are
harmless because every payload is a full object.

### Telemetry streams

```json
{ "op": "subscribe", "topic": "telemetry", "sysid": 1, "compid": 1,
  "fields": ["roll", "pitch", "yaw", "alt", "lat", "lng", "groundspeed", "mode", "armed"], "rate": 20 }
```

- `fields`: names from `GET /telemetry/fields`; omit for all ~1000 (not recommended above a few Hz).
- `rate`: snapshots per second, clamped to 1–50, default 10. The backend samples `CurrentState` at that rate;
  the underlying data changes at the vehicle's stream rates (ATTITUDE 10 Hz by default, GLOBAL_POSITION_INT
  3 Hz...), so a 20 Hz subscription will show repeated values between packets. Raise the vehicle's stream rates
  with `SR0_*` parameters or `MAV_CMD_SET_MESSAGE_INTERVAL` if a smoother HUD is needed.
- The server sends **only fields whose value changed** since the last frame, plus a **full snapshot first and
  every 5 s** (`full: true`) so a client that dropped frames resynchronises. Merge each frame into your local
  state object:

```json
{ "topic": "telemetry", "sysid": 1, "compid": 1, "full": true,  "data": { "roll": 0.07, "pitch": 0.06, "yaw": 354.3, "alt": 0.0, "lat": -35.363261, "lng": 149.16523, "groundspeed": 0.02, "mode": "Stabilize", "armed": false } }
{ "topic": "telemetry", "sysid": 1, "compid": 1, "full": false, "data": { "roll": 0.09, "yaw": 354.5 } }
```

- Frames stop silently while the vehicle is not connected (no error) and resume when it is back; the first
  frame after that is a full one. Re-subscribing for the same `sysid/compid` replaces the stream (new fields or
  rate) without a gap.
- Enum fields arrive as their names (`"mode": "Guided"`); `datetime`/`timespan` fields as ISO strings; NaN as the
  string `"NaN"` (see JSON conventions).

### Raw MAVLink stream

```json
{ "op": "subscribe", "topic": "mavlink", "sysid": 1, "msgs": ["ATTITUDE", "GPS_RAW_INT", 147] }
```

Every matching decoded packet is forwarded:

```json
{ "topic": "mavlink", "linkId": 1, "sysid": 1, "compid": 1, "msg": "ATTITUDE",
  "data": { "time_boot_ms": 123456, "roll": 0.0012, "pitch": -0.0004, "yaw": 1.57, "rollspeed": 0, "pitchspeed": 0, "yawspeed": 0 } }
```

`msgs` takes message names (case-insensitive) or numeric ids; omit `sysid`, `compid` or `msgs` to match all.
`data` is the message struct with MAVLink field names and raw units (radians, degE7, cm...). This is the escape
hatch for anything `CurrentState` does not expose (custom messages, companion-computer traffic, ESC telemetry
detail); an unfiltered subscription on a busy link is hundreds of frames per second, so filter.

### Manual control over the socket

Send frames at 10–50 Hz from your input loop; the backend repeats the latest frame at 20 Hz and releases the
overrides 500 ms after the last one (also when the socket closes):

```json
{ "op": "manual_control", "sysid": 1, "compid": 1, "normalized": { "1": 0.12, "2": -0.05, "3": -1, "4": 0 } }
{ "op": "manual_control_stop", "sysid": 1, "compid": 1 }
```

### Back-pressure and reconnect

Each client has an outbound queue of 2000 frames; when a client cannot keep up the **oldest frames are
dropped** rather than blocking the backend (telemetry resynchronises on the next full frame, events may be
missed, so refresh via REST after a stall). The backend does not reconnect for you: on `close`, reconnect with
back-off and re-send every `subscribe` (the TypeScript `BackendSocket` does this and restores subscriptions).

---

## 5. Data model reference

Field names as they appear on the wire (camelCase). `?` marks nullable.

| Type | Fields |
|---|---|
| `LinkInfo` | `id: int`, `connection: string`, `type: Serial\|Tcp\|Udp\|UdpClient\|WebSocket`, `state: Connecting\|Connected\|Disconnected\|Failed`, `error?: string`, `connectedAt?: datetime`, `tlogPath?: string`, `vehicles: { sysId, compId }[]` |
| `VehicleInfo` | `linkId`, `sysId`, `compId`, `type` (MAV_TYPE), `autopilot` (MAV_AUTOPILOT), `firmware`, `version`, `mode`, `armed: bool`, `linkQuality: int`, `lastPacket: datetime`, `paramsReceived: int`, `paramsTotal: int` |
| `Job` | `id`, `kind`, `title`, `linkId?`, `state: Running\|Succeeded\|Failed\|Cancelled`, `progress: int (-1..100)`, `message?`, `error?`, `result?: any`, `startedAt`, `finishedAt?` |
| `CommandResult` | `accepted: bool`, `message?: string` |
| `StatusTextEntry` | `time: datetime`, `text` |
| `StatusMessage` (ws `status`) | `linkId`, `sysId`, `compId`, `severity: MAV_SEVERITY_*`, `text`, `time` |
| `FlightMode` | `number: int`, `name` |
| `TelemetryField` | `name`, `type: number\|integer\|bool\|string\|enum\|datetime\|timespan`, `displayText?`, `group?` |
| `ParamValue` | `name`, `value: double`, `type` (MAV_PARAM_TYPE), `default?: double` |
| `ParamMetadata` | `name`, `displayName?`, `description?`, `units?`, `min?`, `max?`, `increment?`, `values?: { value: long, name }[]`, `bitmask?: { value, name }[]`, `user?` (`Standard`/`Advanced`), `rebootRequired: bool`, `readOnly: bool` |
| `ParamSetResult` | `name`, `accepted: bool`, `value?`, `error?`, `rebootRequired: bool` |
| `ParamDiff` | `name`, `vehicleValue?`, `fileValue` |
| `MissionPlan` | `type: MISSION\|FENCE\|RALLY`, `home?: { lat, lng, alt }`, `items: MissionItem[]` |
| `MissionItem` | `command: ushort`, `commandName?`, `frame: MAV_FRAME`, `param1..param4: float`, `lat`, `lng`: double, `alt: float` |
| `MissionFileContent` | `format`, `mission?`, `fence?`, `rally?` |
| `FtpEntry` | `name`, `path`, `isDirectory: bool`, `size: ulong`, `modifiedUtc?` |
| `OnboardLog` | `id: ushort`, `size: uint`, `timeUtc?` |
| `LocalLogFile` | `path` (relative), `kind: bin\|tlog\|rlog`, `size`, `modifiedUtc` |
| `Elevation` | `lat`, `lng`, `alt?`, `status: Valid\|Ocean\|Unavailable`, `source`, `geoidUndulation` |
| `ProfilePoint` | `distance`, `lat`, `lng`, `alt?` |
| `MapProviderInfo` | `name`, `id: guid`, `minZoom`, `maxZoom` |
| `CalibrationStatus` | `kind: Accel\|Compass\|Radio`, `sysId`, `compId`, `state: Running\|WaitingForUser\|Succeeded\|Failed\|Cancelled`, `message?`, `position?`, `compasses?: { id, percent, status, attempt }[]`, `reports?: { id, status, fitness, ofsX, ofsY, ofsZ, autosaved }[]`, `channels?: { channel, value, min, max }[]`, `updated` |
| `ManualControlInput` | one of `channels?: (int\|null)[]`, `normalized?: { [channel: number]: number }`, `manual?: { x, y, z, r, buttons }` |
| `ManualControlStatus` | `sysId`, `compId`, `active: bool`, `mode`, `lastInput: datetime`, `channels: int[]` |
| `RtkStartRequest` | `type: Ntrip\|Serial\|Tcp\|Udp\|UdpClient`, `url?`, `host?`, `port?: string`, `baud: int = 115200`, `ntripV1: bool`, `rtcmMessage: bool = true` |
| `RtkStatus` | `running`, `source?`, `error?`, `bytesReceived`, `bpsIn`, `bpsSent`, `injected`, `messages: { [rtcmId]: count }`, `lastSeen: { gps, glonass, galileo, beidou, base }` (seconds), `basePosition?: { lat, lng, alt }`, `surveyIn?: { valid, active, duration, observations, meanAccuracy }` |
| `Aircraft` | `icao`, `callSign?`, `lat`, `lng`, `alt` (m), `heading`, `speed` (cm/s), `verticalSpeed`, `squawk`, `threatLevel`, `source: vehicle\|external`, `lastSeen`, `distance?` |
| `AdsbConfig` | `enabled: bool`, `server?`, `port: int = 30003`, `forwardToVehicle: bool = true` |

---

## 6. Building a GCS UI on this API

Practical guidance distilled from the reference UI (`dev.html`) and the MAUI glass-cockpit app that sits on the
same backend (`MPBackend.UI`, which uses the in-process `Backend` through the same operations).

### Start-up and connection screen

1. `GET /info` to verify the backend (and show its data directory / version in an "about" view).
2. Open the WebSocket and subscribe to `links`, `vehicles`, `status`, `jobs` immediately; keep the socket for
   the lifetime of the page.
3. `GET /links` and `GET /vehicles` to render what is already connected (the backend may have auto-connected,
   or another UI may have opened links; the API is multi-client).
4. Offer `GET /serialports` plus the connection string presets (`udp:14550`, `tcp:...`). On connect, `POST /links`
   and show the returned job's `message` until it leaves `Running`; a `Failed` job means "no heartbeat", keep the
   link row with its `error` and offer retry (delete + post again).
5. Treat `vehicles` `detected` as "add a vehicle tab"; the parameter download that follows is a `params.download`
   job whose progress you can show on the tab (also `paramsReceived/paramsTotal` in `VehicleInfo`).

### Flight display

- One telemetry subscription per displayed vehicle with an explicit `fields` list at 10–20 Hz for the HUD, and a
  second one at 1–2 Hz for slow data (battery, GPS counts, EKF) if you want to keep frames small. Maintain one
  state object per vehicle and merge frames into it; render from the merged object, never from a single frame
  (frames only contain changed fields).
- `mode`, `armed`, `landed_state`, `ekfstatus`, `gpsstatus`, `satcount`, `battery_voltage`,
  `battery_remaining`, `linkqualitygcs`, `failsafe` are the usual annunciators; `messageHigh` holds the latest
  high-severity STATUSTEXT, but the `status` topic is the better source for a message list because it carries
  the severity and arrives for every message.
- Wind, vibration, prearm (`prearmstatus`), terrain (`ter_curalt`, `ter_alt`) and navigation (`wp_dist`,
  `nav_bearing`, `target_bearing`, `xtrack_error`, `wpno`) are all plain fields; see the appendix.
- Attitude in `CurrentState` is degrees; `yaw` is heading 0–360. `alt` is relative to home, `altasl` AMSL.

### Map

- Draw tiles from `/maps/tiles/{provider}/{z}/{x}/{y}`; list providers once from `/maps/providers`. Put the
  backend's map mode on your settings page (`CacheOnly` for the field).
- Ownship from `lat`, `lng`, `yaw`, `groundcourse`; home from the mission `home` (or the `HomeLocation` is not
  a simple field, so read `GET .../missions/mission` once after connect and on `missions` events).
- Mission legs come from `GET .../missions/mission`, fence from `.../fence`, rally from `.../rally`; redraw on
  `missions` topic events (other clients may upload). The active item is `wpno`.
- "Fly here": `POST .../goto` with the clicked position and a UI-chosen altitude; the vehicle must be in Guided
  (`POST .../mode` first; `accepted: false` tells you why not).

### Mission editor

- Edit a `MissionPlan` locally and `PUT` it as a whole; the backend uploads the full list (partial updates are
  not part of the mission protocol). Keep `home` unset unless the user explicitly edits it.
- Use `commandName` in the editor model and let `command` be 0; the backend resolves names. Mission Planner's
  `mavcmd.xml` (in the repo root) documents the per-command meaning of `param1..4` for building item forms.
- Load/save files with `/missions/parse` and `/missions/export` (client-side file picker → raw body with
  `?filename=`); `.plan` carries fence and rally too.
- Altitude profile: `POST /terrain/profile` with the leg polyline, plus `GET /terrain/elevation` for the home
  point; both may return `Unavailable` for a few seconds while tiles download, so poll or show "loading".

### Parameter editor

- Wait for the `params` `loaded` event (or `paramsTotal > 0 && paramsReceived == paramsTotal`), then
  `GET .../params?metadata=true` once and build a virtualised list; metadata gives you labels, descriptions,
  ranges, enum values and bitmasks for proper input widgets.
- Single edits: `PUT .../params/{name}` and update the row from `ParamSetResult.value` (the vehicle may clamp).
  Bulk "write modified": `POST .../params/write`, track the job, then show `result.failed` and offer a reboot
  for `result.rebootRequired`.
- Keep subscribing to `params` `changed` events: parameters change behind your back (another client, onboard
  Lua scripts, calibration) and the event carries the new values.
- Compare/load files: `POST .../params/compare` to show a diff table, then write the selected rows with `/write`.

### Calibration pages

Subscribe to `calibration` before starting; drive the page from `CalibrationStatus` only (state machine:
`Running` → `WaitingForUser` (accel, show `position`) → ... → `Succeeded|Failed|Cancelled`). Offer `cancel`
everywhere. For compass calibration render one progress bar per entry in `compasses[]` and the fitness table from
`reports[]` at the end, then prompt for `reboot`. Radio calibration is a live view of `channels[]` with min/max
bars and a "done" button that calls `radio/finish`.

### Manual control / gamepad

Read the Gamepad API in a `requestAnimationFrame` or 20 ms timer loop and send `manual_control` frames over the
socket with `normalized` values (axis → channel mapping is a UI concern; Mode 2 is `1: roll, 2: pitch,
3: throttle, 4: yaw`). Stop sending to release, or send `manual_control_stop` for an immediate release. Always
show an "overrides active" indicator driven by `GET .../manual-control` / the fact that you are sending.

### Multiple vehicles and multiple clients

Everything is keyed by `sysid/compid`; nothing in the API is "the selected vehicle" except the RTK GGA position
and ADS-B distance (they follow the backend's active vehicle). Several UIs may be connected at once; the
event topics keep them consistent. There is no locking: two clients writing parameters or missions at the same
time are serialised per link by the backend but will overwrite each other logically.

### Error handling checklist

- 404 on a vehicle route after the link dropped: show "vehicle disconnected", keep the tab, wait for a
  `vehicles` `detected` event (same `sysId/compId`) to re-enable it.
- 502 / 499: the vehicle did not answer; retry is safe for reads, ask the user for writes.
- `accepted: false`: show `message` verbatim (ArduPilot's reasons are user-readable, e.g. "PreArm: ...").
- WebSocket `error` frames: a bug in the request, log them in development.
- Numeric fields can hold the string `"NaN"` (see conventions); guard arithmetic and formatting against it.

### What the backend does not do

Firmware flashing and board detection, dataflash log analysis, tlog playback, video, speech, DirectInput
joysticks (use the browser Gamepad API), DroneCAN, follow-me and Mission Planner's survey grid are not
available through this API (see "Known limitations" in `README.md`); the spray-grid planner (3.16) is the one
mission generator. The raw `mavlink` stream and `POST .../command` cover many ad-hoc needs that the typed
endpoints do not.

---

## 7. Appendix: useful telemetry fields

A working subset of the ~1000 names returned by `GET /telemetry/fields` (names are case-insensitive in
`fields` lists; the snapshot returns them with the exact casing below). Units are Mission Planner's.

| Group | Fields |
|---|---|
| Attitude | `roll`, `pitch`, `yaw` (deg, yaw 0–360), `groundcourse` (deg), `nav_roll`, `nav_pitch`, `nav_bearing`, `target_bearing` (deg) |
| Position | `lat`, `lng` (deg), `alt` (m above home), `altasl` (m AMSL), `ter_curalt` (m above terrain), `ter_alt` (terrain height), `DistToHome` (m), `distTraveled` (m), `xtrack_error` (m) |
| Speed | `groundspeed`, `airspeed`, `verticalspeed`, `climbrate` (m/s), `vx`, `vy`, `vz` (m/s NED), `speedup`, `targetalt` |
| Navigation | `mode`, `armed`, `landed`, `landed_state`, `wpno` (active item), `wp_dist` (m), `alt_error`, `aspd_error`, `failsafe`, `timeInAir` (s), `prearmstatus` |
| GPS | `gpsstatus` (fix type), `satcount`, `gpshdop`, `gpstime`, `gpsstatus2` (second GPS), `fixedp` (RTK fixed) |
| EKF | `ekfstatus` (0–1 worst variance), `ekfvelv`, `ekfposvert`, `ekfcompv`, `ekfflags` |
| Battery | `battery_voltage`, `current` (A), `battery_remaining` (%), `battery_usedmah`, `battery_voltage2`, `battery_remaining2`, `battery_cell1`.., `battery_temp`, `battery_kmleft`, `battery_mahperkm` |
| Radio / link | `rssi`, `remrssi`, `noise`, `localsnrdb`, `txbuffer`, `rxerrors`, `linkqualitygcs` (%), `ch1in`..`ch16in`, `ch1out`..`ch16out`, `ch3percent`, `raterc`, `rateattitude`, `rateposition`, `ratesensors` |
| Wind / air | `wind_dir`, `wind_vel` |
| Sensors | `ax`, `ay`, `az`, `gx`, `gy`, `gz`, `mx`, `my`, `mz`, `vibex`, `vibey`, `vibez`, `vibeclip0..2`, `sonarrange`, `rangefinder1`, `sonarvoltage`, `opt_m_x`, `opt_m_y` |
| Power / system | `hwvoltage`, `boardvoltage`, `freemem`, `load` (%), `datetime`, `capabilities` |
| Powertrain | `rpm1`, `rpm2`, `esc1_rpm`, `esc1_volt`, `esc1_curr`, `esc1_temp` (…`esc8_*`), `gen_status` |
| Messages | `messageHigh` (last high-severity STATUSTEXT), `message` (last STATUSTEXT) |

Composite values (home location, planned home, tracker location) are objects, not simple fields, and are not
part of snapshots; read them through the mission (`home`) and vehicle endpoints instead.
