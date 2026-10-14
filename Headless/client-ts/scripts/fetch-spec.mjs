// Refresh openapi.json from a running backend: MPBACKEND_URL=http://127.0.0.1:5880 npm run fetch-spec
import { writeFile } from "node:fs/promises";
const base = process.env.MPBACKEND_URL ?? "http://127.0.0.1:5880";
const res = await fetch(`${base}/swagger/v1/swagger.json`);
if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
await writeFile(new URL("../openapi.json", import.meta.url), JSON.stringify(await res.json(), null, 2) + "\n");
console.log("openapi.json updated from", base);
