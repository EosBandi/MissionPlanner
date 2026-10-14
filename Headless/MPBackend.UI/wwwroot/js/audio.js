// Alert tones synthesised with Web Audio (no files, works offline). The context is created lazily and resumed on
// the first user gesture because WebViews start it suspended.
let ctx = null;
let gain = null;

function context() {
  if (!ctx) {
    ctx = new (window.AudioContext || window.webkitAudioContext)();
    gain = ctx.createGain();
    gain.gain.value = 0.5;
    gain.connect(ctx.destination);
  }
  if (ctx.state === "suspended")
    ctx.resume();
  return ctx;
}

export function prime() {
  const resume = () => { try { context(); } catch (e) { /* no audio device */ } };
  for (const ev of ["pointerdown", "keydown", "touchstart"])
    document.addEventListener(ev, resume, { passive: true });
  resume();
  return !!ctx;
}

// steps: [[frequencyHz, durationMs, gapMs], ...]; returns the context state so .NET can log a blocked context
export function play(steps, volume) {
  const c = context();
  let t = c.currentTime + 0.02;
  for (const [freq, ms, gap] of steps) {
    const osc = c.createOscillator();
    const env = c.createGain();
    osc.type = "sine";
    osc.frequency.value = freq;
    env.gain.setValueAtTime(0, t);
    env.gain.linearRampToValueAtTime(volume ?? 1, t + 0.01);
    env.gain.setValueAtTime(volume ?? 1, t + ms / 1000 - 0.02);
    env.gain.linearRampToValueAtTime(0, t + ms / 1000);
    osc.connect(env).connect(gain);
    osc.start(t);
    osc.stop(t + ms / 1000 + 0.01);
    t += (ms + (gap ?? 0)) / 1000;
  }
  return c.state;
}
