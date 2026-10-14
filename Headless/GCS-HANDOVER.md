# GCS app handover (state as of 2026-10-15)

Where the glass-cockpit GCS on top of the headless backend stands, how to run and test it, the decisions behind it,
and what is left. Read `README.md` for the backend and the per-feature sections; this file is the continuation
guide.

## Decision

The UI must run on **Windows and standalone Android with a USB OTG telemetry radio**, so the backend runs
**in-process** (`MPBackend.Core` referenced directly). Kestrel / `MPBackend.Server` cannot be hosted in a
`net*-android` app (no runtime pack), so the server stays desktop-only and the screens talk to the backend
through the `IGcs` interface (`MPBackend.UI/Services/IGcs.cs`), implemented by `InProcessGcs`. A remote
(REST / WebSocket) implementation can be added later for a browser or multi-monitor front without touching
the screens.

Technology: **.NET MAUI Blazor Hybrid** (`MPBackend.App`, `net10.0-windows` + `net10.0-android`) hosting a Razor
class library (`MPBackend.UI`). Instruments are **Razor SVG components** (no JavaScript map or instrument
library). The design source is `D:\00GIT\UI\gcs-design-guide\gcs-design-spec.json` (glass-cockpit spec);
three generators keep the app in sync with it:

| Script | Output |
|---|---|
| `MPBackend.UI/tools/gen-tokens.py <spec>` | `wwwroot/css/tokens.css` (colours, type sizes, motion, spacing) |
| `MPBackend.UI/tools/gen-fma.py <spec>` | `Pfd/FmaTable.g.cs` (mode → flight mode annunciator) |
| `MPBackend.UI/tools/gen-alerts.py <spec>` | `Alerts/AlertTable.g.cs` (CAS alert table) |

## What exists (steps 1–8, all committed on branch `headless-gcs`)

1. **Shell**: status strip (master lamps, vehicle, ARM, mode, phase chip, SIMULATION badge, FLT, BAT, LINK, GNSS,
   EKF, UTC), PFD and MFD stages, 12-slot soft-key bars with hold-to-confirm for critical keys, the spec's
   desktop / tablet landscape / tablet portrait / phone layouts, MFD page groups MAP · MSN · VEH · AUX with a page
   locator, keyboard navigation (←/→ page, Ctrl+←/→ group, hold Esc = home, K acknowledge). Pages: VEH › LINK
   (connect / disconnect, serial ports, active vehicle), AUX › SYSTEM STATUS (jobs, STATUSTEXT).
2. **PFD** (`Pfd/`): attitude, speed and altitude tapes with profile colour bands and reference marks, VSI, HSI,
   FMA, nav status bar, VDI, wind / OAT / flight-time / HOME windows, hazard plates. Fed at 20 Hz by `PfdFeed`;
   each instrument re-renders only when its own values change (`PfdPart.Key()`). `VehicleProfile` takes
   thresholds from vehicle parameters (both the cm/s names and Copter 4.7's m/s names).
3. **CAS** (`Alerts/`): generated alert table, `AlertService` (telemetry, SYS_STATUS health, parameters and
   STATUSTEXT triggers; ordering, inverse video, escalation, connect policy, phase inhibits, history), lamps, CAS
   window in the MFD corner, ALERTS soft key, alerts window with ACK / RCL / CLOSE.
4. **Nav Map 1** (`Map/`, `Pages/NavMapPage.razor`): backend tiles (GMap.NET providers + cache) laid out in C#
   as data-URI images, SVG overlay (ownship, track vector, home, mission legs, waypoints, rally, fences), range
   steps, NORTH / TRK / HDG UP, declutter, pan cursor with plate, MEASURE, FLY HERE, provider menu, range ring.
5. **Active Mission** (`Pages/MissionPage.razor`): leg table with DTK / DIS / ETE, active row, cursor row,
   SET WP (hold), PAUSE / CONTINUE, RELOAD.
6. **Parameters** (`Pages/ParametersPage.razor`): virtualised list with apm.pdef.xml metadata, ranked search,
   pending edits written as one job (hold), per-row results, REVERT / MODIFIED / REFRESH, EXPORT (.param under the
   app data dir), COMPARE a .param / .parm / QGC file and APPLY FILE. Numeric parameters with named suggestions
   get a free number field with a datalist.
7. **Selected values** (`Pfd/SelectedValues.cs`): knob modes HDG / CRS / ALT / V/S / SPD (keys H C A V P, tap an
   instrument, click a value box), turn (↑/↓, Shift ×10, wheel, drag on a tape), sync (S, middle button, second
   tap), popup left of the HSI, auto-send in GUIDED after 1.5 s or Enter, ACK → solid bug, NACK → yellow flash and
   revert. HDG = CONDITION_YAW / GUIDED_CHANGE_HEADING, ALT = guided reposition / GUIDED_CHANGE_ALTITUDE,
   SPD = DO_CHANGE_SPEED; CRS and V/S are references.
8. **Aural** (`Alerts/AuralService.cs`, `wwwroot/js/audio.js`, `MPBackend.App/MauiVoice.cs`): Web Audio tones,
   platform text-to-speech, warning repeat every 5 s until acknowledged, caution once, altitude approach /
   deviation tones, "Minimum, minimum", MUTE soft key.

Everything was verified on Windows against the ArduCopter SITL. **Android compiles but has never run on a
device** (USB OTG serial is postponed, see backlog).

## Running and testing

```bash
dotnet build Headless/MPBackend.App -f net10.0-windows10.0.19041.0     # bin/Debug/net10.0-windows10.0.19041.0/win-x64/MPBackend.App.exe
dotnet build Headless/MPBackend.App -f net10.0-android                  # needs the android workload (.NET 10 SDK)
```

Development environment variables read by `MauiProgram`:

| Variable | Effect |
|---|---|
| `MPGCS_AUTOCONNECT=tcp:127.0.0.1:5760` | open a link at startup |
| `MPGCS_DEV_FLY=30` | upload a 4-waypoint mission around home and fly it in AUTO (takeoff altitude 30 m) |
| `MPGCS_DEV_GPSLOSS=1` | additionally switch the simulated GPS off for 45 s after take-off (CAS exercise) |
| `MPGCS_MAPCACHE=<gmapcache dir>` | reuse an existing tile cache (`Headless/.devdata/gmapcache` has CMAC imagery) |

`tools/Start-Sitl.ps1 -Restart` runs the SITL that Mission Planner's Simulation screen downloaded (tcp:5760,
restarts when the client disconnects). `tools/Start-GcsApp.ps1` sets the variables above and launches the app;
`tools/Get-AppScreenshot.ps1` captures the app window (used for the automated checks; synthetic clicks on the
soft keys at 1600×900 are at x = 41 + 54.4·i for the PFD bar and 710 + 76·i for the MFD bar, y = 860).

Logs: `%LOCALAPPDATA%\User Name\org.ardupilot.mpgcs\Data\MPBackend\logs\` — `mpbackend.log` (backend, log4net)
and `app.log` (UI, render errors, aural events, dev flight). Parameter exports go to `...\Data\MPBackend\params`.

Notes for the SITL in use (ArduCopter master / 4.7): speed parameters are in m/s with new names (`WP_SPD`,
`LOIT_SPEED_MS`, `PILOT_SPD_UP`, `LAND_SPD_MS`, `ATC_ANGLE_MAX`), the simulated GPS switch is `SIM_GPS1_ENABLE`,
and `AUTO_OPTIONS=3` lets the dev flight arm and take off in AUTO.

## Conventions

- Screens get backend data only through `IGcs`; add a method there and in `InProcessGcs` rather than reaching
  into `Backend` from a component.
- Singletons that fan events out (`GcsSession`, `PfdFeed`, `AlertService`, `SelectedValues`, `MissionStore`,
  `TileCache`, `AuralService`) raise `Changed` on backend threads; components re-render with
  `InvokeAsync(StateHasChanged)`. Services needing the WebView's JS runtime get it from `GcsShell`
  (`AttachAsync`).
- PFD instruments inherit `PfdPart` and declare what they draw in `Key()`; SVG text goes through the `T`
  component because Razor reserves `<text>`; numbers in SVG attributes go through `Svg.F()`.
- MFD pages receive `MfdDisplay` as a cascading parameter and publish their keys with `SetSoftKeys` (12 slots,
  ALERTS is always slot 12), their range with `SetRange`, and keyboard handlers through `ShellState.PageKeys`.
- Colours and sizes come from `tokens.css` variables (`--c-*` semantic, `--p-*` palette); do not hard-code.
- Deviations from the spec are listed in the README sections (phone soft-key grid 6×2, attitude stale after 1 s,
  orientation plate top-left because the CAS owns the top-right corner, turn rate derived from heading).

## Backlog (in the order I would take them)

1. **Android USB OTG serial** (postponed): port `ExtLibs/UsbSerialForAndroid` to an SDK-style `net10.0-android`
   binding, bring `ExtLibs/Xamarin/Xamarin.Android/AndroidSerial.cs` over, register `SerialPort.DefaultType` and
   `SerialPort.GetCustomPorts` (the hooks the old Xamarin app used), USB device filter, foreground service with a
   wake lock, then run on a device. Also Release-mode trimming (reflection in `TelemetryFields`, Newtonsoft,
   log4net).
2. **Terrain and traffic**: map layers from the backend's terrain and ADS-B services, hazard plates and the
   TERRAIN / TRAFFIC alerts, TRAFFIC / TERRAIN soft keys.
3. **Calibration flows** (accel, compass, radio) and **onboard log download** from the backend's calibration and
   log services; the AUX › LOGS and setup pages.
4. **Checklists** and the flight-phase automation (auto page per phase), SETUP page (units, brightness, PFD
   options), MSG flag on the HSI.
5. Nav map: pinch zoom, DTK UP, the configurable data-fields column, NAV MAP 2 / SPLIT / vertical profile.
6. PFD: V/S velocity target for copters (needs repeated SET_POSITION_TARGET), a SEND soft key for touch-only use,
   numeric keypad on long-press, BRG2 / GCS position, mission-derived AUTO annunciations, phone map mode with the
   compact PFD strip.
7. Bundle the Roboto Condensed font for offline use (currently loaded from Google Fonts).
