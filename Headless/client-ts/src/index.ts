/**
 * TypeScript client for the Mission Planner headless backend.
 *
 * - REST: `createBackendClient()` returns a fully typed openapi-fetch client generated from openapi.json.
 * - WebSocket: `BackendSocket` handles subscriptions, telemetry delta merging, raw MAVLink, manual control
 *   and automatic reconnect (subscriptions are restored after a reconnect).
 */
import createClient, { type Client } from "openapi-fetch";
import type { components, paths } from "./schema";

export type { components, paths } from "./schema";

type S = components["schemas"];
export type LinkInfo = S["Core.Links.LinkInfo"];
export type LinkState = S["Core.Links.LinkState"];
export type VehicleInfo = S["Core.Vehicles.VehicleInfo"];
export type Job = S["Core.Jobs.Job"];
export type JobState = S["Core.Jobs.JobState"];
export type ParamValue = S["Core.Params.ParamValue"];
export type ParamMetadata = S["Core.Params.ParamMetadata"];
export type MissionPlan = S["Core.Missions.MissionPlan"];
export type MissionItem = S["Core.Missions.MissionItem"];
export type CalibrationStatus = S["Core.Calibration.CalibrationStatus"];
export type ManualControlInput = S["Core.Control.ManualControlInput"];
export type RtkStatus = S["Core.Gps.RtkStatus"];
export type Aircraft = S["Core.Gps.Aircraft"];
export type TelemetryField = S["Core.Telemetry.TelemetryField"];

export type BackendClient = Client<paths>;

/** Typed REST client, e.g. `await api.GET("/api/v1/vehicles")`. */
export function createBackendClient(baseUrl = "http://127.0.0.1:5880", init?: RequestInit): BackendClient {
  return createClient<paths>({ baseUrl, ...init });
}

/** Event topics that carry `{ topic, data }` envelopes. */
export type EventTopic = "links" | "vehicles" | "status" | "jobs" | "params" | "missions" | "calibration" | "rtk" | "adsb";

export interface StatusText {
  linkId: number;
  sysId: number;
  compId: number;
  severity: string;
  text: string;
  time: string;
}

export interface EventPayloads {
  links: { event: string; link: LinkInfo };
  vehicles: { event: string; linkId: number; sysId: number; compId: number };
  status: StatusText;
  jobs: Job;
  params: { event: string; sysId: number; compId: number; count: number; changed: ParamValue[] | null };
  missions: { event: string; sysId: number; compId: number; type: string; count: number };
  calibration: CalibrationStatus;
  rtk: RtkStatus;
  adsb: Aircraft[];
}

/** CurrentState field values keyed by field name (see GET /api/v1/telemetry/fields). */
export type Telemetry = Record<string, number | string | boolean | null>;

export interface MavlinkMessage {
  linkId: number;
  sysid: number;
  compid: number;
  msg: string;
  data: Record<string, unknown>;
}

type Unsubscribe = () => void;

interface TelemetrySub {
  sysid: number;
  compid: number;
  fields?: string[];
  rate: number;
  state: Telemetry;
  handlers: Set<(state: Telemetry, changed: Telemetry) => void>;
}

/**
 * WebSocket connection to /ws with automatic reconnect.
 *
 * ```ts
 * const ws = new BackendSocket("http://127.0.0.1:5880");
 * ws.on("status", s => console.log(s.text));
 * ws.telemetry(1, 1, t => hud.update(t), { fields: ["roll", "pitch", "alt"], rate: 20 });
 * ws.manualControl(1, 1, { normalized: { 1: 0, 2: 0, 3: -1, 4: 0 } }); // call at 10-50 Hz
 * ```
 */
export class BackendSocket {
  private ws?: WebSocket;
  private closed = false;
  private retryMs = 500;
  private readonly topics = new Map<string, Set<(data: any) => void>>();
  private readonly telemetrySubs = new Map<string, TelemetrySub>();
  private mavlinkSub?: { filter: { sysid?: number; compid?: number; msgs?: string[] }; handler: (m: MavlinkMessage) => void };
  private readonly openHandlers = new Set<(open: boolean) => void>();

  constructor(baseUrl = "http://127.0.0.1:5880", private readonly socketFactory: (url: string) => WebSocket = u => new WebSocket(u)) {
    this.url = baseUrl.replace(/^http/, "ws").replace(/\/$/, "") + "/ws";
    this.connect();
  }

  private readonly url: string;

  get isOpen(): boolean {
    return this.ws?.readyState === WebSocket.OPEN;
  }

  /** Notified with true/false when the socket opens/closes. */
  onConnection(handler: (open: boolean) => void): Unsubscribe {
    this.openHandlers.add(handler);
    return () => this.openHandlers.delete(handler);
  }

  /** Subscribe to an event topic. */
  on<T extends EventTopic>(topic: T, handler: (data: EventPayloads[T]) => void): Unsubscribe {
    let set = this.topics.get(topic);
    if (!set) {
      set = new Set();
      this.topics.set(topic, set);
      this.send({ op: "subscribe", topic });
    }
    set.add(handler);
    return () => {
      set!.delete(handler);
      if (set!.size === 0) {
        this.topics.delete(topic);
        this.send({ op: "unsubscribe", topic });
      }
    };
  }

  /**
   * Stream CurrentState for a vehicle. The server sends only changed fields (plus a full snapshot every 5 s);
   * the handler receives the merged state and the fields that changed.
   */
  telemetry(sysid: number, compid: number, handler: (state: Telemetry, changed: Telemetry) => void,
            options: { fields?: string[]; rate?: number } = {}): Unsubscribe {
    const key = `${sysid}/${compid}`;
    let sub = this.telemetrySubs.get(key);
    if (!sub) {
      sub = { sysid, compid, fields: options.fields, rate: options.rate ?? 10, state: {}, handlers: new Set() };
      this.telemetrySubs.set(key, sub);
      this.sendTelemetrySub(sub);
    }
    sub.handlers.add(handler);
    return () => {
      sub!.handlers.delete(handler);
      if (sub!.handlers.size === 0) {
        this.telemetrySubs.delete(key);
        this.send({ op: "unsubscribe", topic: "telemetry", sysid, compid });
      }
    };
  }

  /** Raw decoded MAVLink messages (filter by vehicle and message names such as "ATTITUDE"). */
  mavlink(handler: (m: MavlinkMessage) => void, filter: { sysid?: number; compid?: number; msgs?: string[] } = {}): Unsubscribe {
    this.mavlinkSub = { filter, handler };
    this.send({ op: "subscribe", topic: "mavlink", ...filter });
    return () => {
      this.mavlinkSub = undefined;
      this.send({ op: "unsubscribe", topic: "mavlink" });
    };
  }

  /**
   * Send one manual control frame. Call continuously (10-50 Hz, e.g. from a Gamepad API loop); the backend
   * repeats the latest frame at 20 Hz and releases the RC overrides 500 ms after frames stop.
   */
  manualControl(sysid: number, compid: number, input: ManualControlInput): void {
    this.send({ op: "manual_control", sysid, compid, ...input });
  }

  /** Release the RC overrides immediately. */
  stopManualControl(sysid: number, compid: number): void {
    this.send({ op: "manual_control_stop", sysid, compid });
  }

  close(): void {
    this.closed = true;
    this.ws?.close();
  }

  private connect(): void {
    if (this.closed) return;
    const ws = this.socketFactory(this.url);
    this.ws = ws;
    ws.onopen = () => {
      this.retryMs = 500;
      // restore subscriptions after (re)connect
      for (const topic of this.topics.keys()) this.send({ op: "subscribe", topic });
      for (const sub of this.telemetrySubs.values()) this.sendTelemetrySub(sub);
      if (this.mavlinkSub) this.send({ op: "subscribe", topic: "mavlink", ...this.mavlinkSub.filter });
      this.openHandlers.forEach(h => h(true));
    };
    ws.onclose = () => {
      this.openHandlers.forEach(h => h(false));
      if (this.closed) return;
      setTimeout(() => this.connect(), this.retryMs);
      this.retryMs = Math.min(this.retryMs * 2, 10_000);
    };
    ws.onmessage = e => this.dispatch(JSON.parse(typeof e.data === "string" ? e.data : ""));
  }

  private dispatch(m: any): void {
    switch (m.topic) {
      case "telemetry": {
        const sub = this.telemetrySubs.get(`${m.sysid}/${m.compid}`);
        if (!sub) return;
        if (m.full) sub.state = {};
        Object.assign(sub.state, m.data);
        sub.handlers.forEach(h => h(sub.state, m.data));
        return;
      }
      case "mavlink":
        this.mavlinkSub?.handler(m as MavlinkMessage);
        return;
      default:
        this.topics.get(m.topic)?.forEach(h => h(m.data));
    }
  }

  private sendTelemetrySub(sub: TelemetrySub): void {
    this.send({ op: "subscribe", topic: "telemetry", sysid: sub.sysid, compid: sub.compid, fields: sub.fields, rate: sub.rate });
  }

  private send(msg: unknown): void {
    if (this.ws?.readyState === WebSocket.OPEN) this.ws.send(JSON.stringify(msg));
  }
}

/**
 * Wait for a job (e.g. a param download or mission upload started via REST) to finish.
 */
export async function waitForJob(api: BackendClient, id: string, pollMs = 250): Promise<Job> {
  for (;;) {
    const { data, error } = await api.GET("/api/v1/jobs/{id}", { params: { path: { id } } });
    if (error || !data) throw new Error(`job ${id} not found`);
    if (data.state !== "Running") return data;
    await new Promise(r => setTimeout(r, pollMs));
  }
}
