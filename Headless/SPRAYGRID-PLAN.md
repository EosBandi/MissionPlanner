# SprayGrid standalone app: plan

Rewrite of the `ExtLibs/Spraygrid` Mission Planner plugin (SoleonPlannerOld, "SprayGrid-Soleon") as a
standalone desktop program with a modern UI, using the headless Mission Planner backend for the geometry and
mission-building work. Written 2026-11-12 after reviewing the plugin and the backend; the decisions below were
taken with the user.

## Decisions

| Topic | Decision |
|---|---|
| Backend | `mpheadless/Headless` (MPBackend.*) first; port the new endpoint to `D:\00Git\ACCBackend` later |
| UI | Web: Vite + React + TypeScript, **CesiumJS** with Mapbox imagery and terrain, `@mpbackend/client` for the API |
| Packaging | **Tauri 2** exe (WebView2) that starts the backend as a sidecar on a local port |
| Inputs | Draw the field polygon, obstacles and home on the map; import KML/KMZ, GeoJSON, `.poly`, old `.grid` |
| Output | **Files only**: `.waypoints`, `.plan` (QGC JSON), `.grid`. No vehicle link in v1 (the backend can do it later) |
| Sprayer protocol | Unchanged: `DO_SEND_SCRIPT_MESSAGE` (217) id 1 = setup (L/ha, line distance, speed); id 2 = on (3, distance to next) / off (0) |
| Elevation for altitude tracking | User selectable: backend SRTM/DTED (default, offline) or Cesium terrain sampled in the browser |
| Home | Draggable marker, stored in `.grid`, exported as item 0 |
| Source location | New repo `D:\00Git\SprayGrid` for the app; backend changes in mpheadless (branch `headless-gcs`) |

## What the old plugin does (to be preserved)

Inputs: Flight Planner polygon, fence overlay polygons and circles as obstacles (circles become octagons),
planned home. Parameters: line distance (default from area), angle (first side; flip 180), shift, lane
separation, start position (Home/BottomLeft/.../Point), extended endpoint, L/ha, spray bar width, fly speed,
altitude + GPS offset, altitude reference (Relative/Absolute/Terrain), altitude tracking + max error, takeoff
altitude, add TAKEOFF/RTL, head lock (CONDITION_YAW), delay at WP, split None/Time/Segments, expand obstacles
by bar width, ignore fences. Display toggles: boundary, S/E markers, grid.

Core: `Grid.CreateSprayGrid(polygon, alt, distance, angle, startpos, laneSep, home, obstacles, shift, barwidth,
extended)` in the old `ExtLibs/Utilities/Grid.cs` (about 650 lines including the Clipper2 inflate helpers;
points tagged S/E/I/M). It exists only in SoleonPlannerOld.

Mission build (`BUT_Accept_Click2`): 217/1 setup, optional TAKEOFF at home, DO_CHANGE_SPEED, optional
CONDITION_YAW, waypoints (p3 = relative altitude reference when alt tracking, p4 = 1 start / 2 end of strip,
p1 = delay), 217/2 on/off per strip, DO_CHANGE_SPEED back to WPNAV_SPEED, optional RTL. Split by time (marks
`SplitTime` points from a speed/distance model including the return to home) or by N segments (cut at `S`
points); each segment is a full takeoff-to-RTL block with DO_JUMP headers so the vehicle can start at a segment.
Altitude tracking: sample terrain across the spray-bar footprint (0.2 m steps, 0.2 m fore/aft) and take the max;
terrain frame sets per-point altitude; relative/absolute inserts interim points along each leg where the error
exceeds the limit, then prunes collinear points.

Stats shown: area, strips, spray distance/time, total distance/flight time (including home legs), split times.
`.grid` XML (`GridData` via XmlSerializer) save/load; parameters remembered in config.

Dropped: "Refresh map tiles" menu, Flight Planner custom button, display-unit multipliers (the app is metric
with unit formatting in the UI only).

## Architecture

```
SprayGrid.exe  (Tauri 2 shell, WebView2)
 |- ui/          Vite + React + TS, CesiumJS (Mapbox imagery/terrain, backend tiles as offline fallback)
 |               @mpbackend/client (openapi-fetch) -> http://127.0.0.1:<port>/api/v1
 |- sidecar      MPBackend.Server self-contained publish, started with --urls http://127.0.0.1:<free port>
                 data dir %LOCALAPPDATA%\SprayGrid (srtm cache, map cache, log)
      new   POST /planning/spraygrid/preview   grid points + stats
      new   POST /planning/spraygrid/mission   MissionPlan + stats (alt tracking, splits, sprayer commands)
      have  POST /missions/export?format=waypoints|plan, GET /terrain/elevation, GET /maps/tiles/...
```

The algorithm goes into the backend (not into TypeScript) so the proven C# code is ported once, unit-tested
against the old plugin's output, and shared by the later ACCBackend port.

## Phase 1: backend (mpheadless `Headless/`) — done 2026-11-12

Implemented in `MPBackend.Core/Planning` (`SprayGridGenerator`, `SprayPlanner`, `ElevationSampler`,
`SprayGridService`), `MPBackend.Server/Api/PlanningEndpoints.cs`, tests in `MPBackend.Tests/Planning`,
API.md section 3.16, client-ts regenerated. Still open: golden comparison against field `.grid` /
`.waypoints` pairs from the old plugin (none available yet).


1. `MPBackend.Core/Planning/SprayGrid.cs`: port `CreateSprayGrid` and the helpers it needs (`inflateVertex`,
   `scaleVertexList`, `resizeVertexList`, and the private MP `Grid` helpers it calls: `getPolyMinMax`,
   `newPointAngleDistance`, `FindLineIntersection`, `findClosestLine`, `linelatlng`, `addtomap` stub) into a new
   class. The MP tree is not edited (the slim libs compile it by link). Add the `Clipper2` package to
   MPBackend.Core. Points keep the S/E/I/M tags and the `SplitTime` marker.
2. `MPBackend.Core/Planning/SprayMissionBuilder.cs`: port the mission build and split logic from
   `SprayGridUI.BUT_Accept_Click2` / `AddWP` into a pure function producing `MissionPlan` (existing model), plus
   the stats (area via UTM shoelace, strips, distances, times, split times). `WPNAV_SPEED` comes from the
   request (optional; no vehicle), as does home.
3. Altitude tracking: port `getVehicleAreaDisplacementPoints`, `getMaxAltinArea`, interim-point insertion and
   pruning behind an `IElevationSampler`: `BackendTerrain` (TerrainService / srtm, waits for tiles) or
   `ClientRaster` (the UI sends a bbox + cols/rows + heights array sampled from Cesium; bilinear lookup). This
   keeps the dense footprint sampling server-side in both modes.
4. Endpoints in a new `MPBackend.Server/Api/PlanningEndpoints.cs`, DTOs in `Dtos.cs`, Swagger descriptions,
   section 3.16 in `API.md`, README feature table and "Known limitations" update. Regenerate
   `client-ts/openapi.json` and `src/schema.d.ts`, add `SprayGridRequest` type aliases.
5. Tests (`MPBackend.Tests/Planning`): golden tests from one or two field `.grid` files and the `.waypoints` the
   old plugin produced for them (to be supplied or generated with the old build); unit tests for split by
   time/segments, obstacle inflation, alt-tracking interim points on synthetic terrain, and `.grid` round trip.

## Phase 2: UI (new repo `D:\00Git\SprayGrid`)

1. Scaffold: `create-tauri-app` (React + TS + Vite), `cesium` + `vite-plugin-cesium`, `@mpbackend/client`
   (file dependency on `mpheadless/Headless/client-ts` or a vendored build), `zustand` state, Tailwind with
   headless UI primitives (dark theme, side panel, cards), `fast-xml-parser` for `.grid`.
2. Layout: full-window Cesium viewer; collapsible left panel with grouped parameters (Grid / Spray / Altitude /
   Mission) mirroring the old controls; stats card; top toolbar: New, Open, Save `.grid`, Export `.waypoints` /
   `.plan`, Settings (Mapbox token, Cesium ion token, elevation source, backend status, units). Any change
   triggers a debounced `preview` call; `mission` runs on Export.
3. Map tools: polygon draw/edit (click to add, drag vertex, insert on edge, delete), obstacle polygons and
   circles, draggable home marker, measure. Rendering as in the old plugin: yellow spray strips, light-blue
   transits, green/red S/E markers, purple I markers, boundary/markers/grid toggles; polylines clamped to
   terrain when terrain is on; 2D/3D toggle.
4. Files: `.grid` reader/writer compatible with the old `GridData` XML (same element names; a new `home` element
   is ignored by the old plugin), KML/KMZ, GeoJSON and `.poly` import for polygon and obstacles; export via the
   backend `/missions/export`; Tauri file dialogs. Settings and last-used parameters persisted
   (`tauri-plugin-store`).
5. Cesium elevation mode: sample the field bbox with `sampleTerrainMostDetailed` at about 1 m spacing into the
   raster the backend accepts (progress indicator; cancellable).

## Phase 3: packaging

- `tools/Publish-Backend.ps1` in the new repo: `dotnet publish` MPBackend.Server self-contained win-x64
  single-file into `src-tauri/binaries/` (Tauri sidecar naming), copying `appsettings.json` with the SprayGrid
  data directory.
- Tauri: pick a free port, spawn the sidecar, poll `GET /info`, show a splash until ready, kill on exit; NSIS
  installer; app icon; version from one place.
- GitHub Actions (windows-latest): build backend publish + `tauri build`, upload the installer artifact.
- Prerequisite on this machine: the Rust toolchain (`rustup`, MSVC target) is not installed yet.

## Phase 4: ACCBackend port (later)

Copy `Planning/` + `PlanningEndpoints.cs` + DTOs + tests into `D:\00Git\ACCBackend\src`, add Clipper2, update
its API reference and client.

## Open items and risks

- Golden reference data: a `.grid` and matching `.waypoints` from the old plugin are needed to prove fidelity.
- Mapbox and Cesium ion tokens must be provided by the user (stored locally, never in the repo).
- The old `Grid.cs` helpers are private in MP; they are copied, so upstream MP changes do not flow automatically.
- The old UI relied on display-unit multipliers (`CurrentState.multiplierspeed/dist`); the new app is metric
  end to end and only formats in the UI.
- `.grid` files store `fences` as `Locationwp` lists; the new app writes an empty list and reads obstacles from
  `obstaclesmarks`.
- Cesium-mode elevation accuracy depends on the sampled raster spacing (1 m default; configurable).
