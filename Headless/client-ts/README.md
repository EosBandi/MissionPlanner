# @mpbackend/client

TypeScript client for the Mission Planner headless backend (`Headless/MPBackend.Server`).

- `createBackendClient(baseUrl)`: [openapi-fetch](https://openapi-ts.dev/openapi-fetch/) client typed from
  `openapi.json` (paths, parameters, bodies and responses).
- `BackendSocket`: WebSocket client for `/ws` with reconnect + re-subscribe, telemetry delta merging, raw MAVLink,
  and `manualControl()` for Gamepad API input.
- `waitForJob(api, id)`: poll a job (connect, param download, mission upload, ...) until it finishes.
- Type aliases for the main models (`VehicleInfo`, `MissionPlan`, `CalibrationStatus`, ...), plus `components` /
  `paths` for everything else.

```bash
npm install
npm run build                 # dist/
npm run fetch-spec            # refresh openapi.json from a running backend (MPBACKEND_URL, default :5880)
npm run generate              # regenerate src/schema.d.ts from openapi.json
node scripts/smoke.mjs        # end-to-end check against a running backend + vehicle
```

See `../README.md` for the API and WebSocket protocol.
