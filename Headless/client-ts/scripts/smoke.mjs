// Smoke test of the built client against a running backend + vehicle (SITL on tcp:5760 by default):
//   npm run build && node scripts/smoke.mjs
import { BackendSocket, createBackendClient, waitForJob } from "../dist/index.js";

const base = process.env.MPBACKEND_URL ?? "http://127.0.0.1:5880";
const connection = process.env.MPBACKEND_CONNECT ?? "tcp:127.0.0.1:5760";
const api = createBackendClient(base);

const ws = new BackendSocket(base);
const statusTexts = [];
ws.on("status", s => statusTexts.push(s.text));
ws.on("links", e => console.log("link event:", e.event, e.link.connection, e.link.state));

let { data: vehicles } = await api.GET("/api/v1/vehicles");
if (!vehicles.length) {
  const { data: connect, error } = await api.POST("/api/v1/links", { body: { connection } });
  if (error) throw new Error(JSON.stringify(error));
  const job = await waitForJob(api, connect.job.id);
  console.log("connect job:", job.state, job.message);
  ({ data: vehicles } = await api.GET("/api/v1/vehicles"));
}
const v = vehicles[0];
console.log("vehicle:", v.sysId, v.compId, v.type, v.firmware, v.version);

let updates = 0, last = {};
const stop = ws.telemetry(v.sysId, v.compId, (state, changed) => { updates++; last = state; },
  { fields: ["mode", "armed", "alt", "lat", "lng", "satcount"], rate: 10 });
await new Promise(r => setTimeout(r, 3000));
stop();
console.log(`telemetry: ${updates} updates in 3 s, state`, last);

const { data: param } = await api.GET("/api/v1/vehicles/{sysid}/{compid}/params/{name}",
  { params: { path: { sysid: v.sysId, compid: v.compId, name: "FRAME_CLASS" } } });
console.log("param:", param);

const { data: mission } = await api.GET("/api/v1/vehicles/{sysid}/{compid}/missions/{type}",
  { params: { path: { sysid: v.sysId, compid: v.compId, type: "mission" } } });
console.log("mission:", mission.items.length, "items, home", mission.home);

const { data: elevation } = await api.GET("/api/v1/terrain/elevation",
  { params: { query: { lat: last.lat, lng: last.lng, wait: 10 } } });
console.log("terrain:", elevation.alt, elevation.status);

console.log("status texts seen:", statusTexts.length);
ws.close();
