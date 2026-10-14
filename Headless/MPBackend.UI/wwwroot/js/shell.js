// Shell helpers: viewport size -> ShellState.SetViewport (layout mode), keyboard focus on the shell root.
export function observe(dotnet, root) {
  const report = () => dotnet.invokeMethodAsync("OnViewport", window.innerWidth, window.innerHeight);
  new ResizeObserver(report).observe(document.documentElement);
  window.addEventListener("orientationchange", report);
  report();
  root.focus();
  // keep keyboard navigation alive after clicking buttons/inputs lose focus
  document.addEventListener("pointerup", e => {
    if (!e.target.closest("input, select, textarea"))
      root.focus({ preventScroll: true });
  });
}

// Element size -> .NET (maps need their pixel size for tile layout).
export function observeSize(dotnet, el, method) {
  const report = () => { const r = el.getBoundingClientRect(); dotnet.invokeMethodAsync(method, r.width, r.height, r.left, r.top); };
  const ro = new ResizeObserver(report);
  ro.observe(el);
  report();
  return { dispose: () => ro.disconnect() };
}

export function focus(el) { el?.focus(); el?.select?.(); }
export function click(el) { el?.click(); }
