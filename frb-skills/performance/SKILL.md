---
name: performance
description: FlatRedBallService.Performance — opt-in rolling FPS/timing/collision stats; StartupProfiler for one-time boot/load timing. Triggers: PerformanceMonitor, GenerateReport, FPS, frame time, DeepCollisionCount, StartupProfiler, ProfileStartup, "why is my game slow", "slow to load", measuring a browser/WASM build, "click didn't register".
---

# Performance Monitoring

`FlatRedBallService.Default.Performance` (a `PerformanceMonitor`, `src/Diagnostics/PerformanceMonitor.cs`) tracks rolling FPS, per-phase frame timing, and a per-collision-relationship severity breakdown over a 120-frame window.

**Off by default** — set `IsEnabled = true` before it records anything; until then every stat reads zero/empty.

```csharp
FlatRedBallService.Default.Performance.IsEnabled = true;

// later, e.g. every N frames from Screen.CustomActivity:
var perf = FlatRedBallService.Default.Performance;
Console.WriteLine(perf.GenerateReport());   // FPS + phase timing + collision severity, as a string
var fps = perf.Fps;                         // .Current / .Average / .Min / .Max
var worst = perf.GetCollisionReport();      // most expensive first; each row carries a PartitionStatus
```

`GenerateReport()` only builds a string — it does no I/O itself, so printing/logging/writing it is on the caller.

## Timer resolution (web)

`GenerateReport()` opens with a `Platform:` line and the measured clock step (`TimerResolutionMs`, probed once via `ProfileClock.MeasureResolutionMs`).

**Landmine:** browsers coarsen their clock as a Spectre mitigation — ~1ms on Firefox/Safari, ~0.1ms on Chrome — and `Stopwatch.Frequency` does not reflect it. Whole-pass totals stay accurate (start/end errors cancel), but any per-phase number smaller than one step reads as 0 or one full step and nothing between. The report warns automatically above 0.5ms. Read `FrameTotal` first, then the phases.

Set `PlatformLabel` from the host — the engine targets `net10.0` and cannot read `navigator.userAgent` itself.

Every row in the collision report carries a `PartitionStatus`. `Unpartitioned` is the only value worth acting on, and the fix is to set the same `Factory<T>.PartitionAxis` on both sides of the relationship. `NotApplicable` means one side is not a factory, such as a `TileShapes`, a single entity, or a plain `List<T>`, so no axis setting would change it. See the `collision-relationships` skill.

See `src/Diagnostics/FrameProfile.cs` for the underlying per-frame timing struct.

## Measuring a browser build

- **`dotnet run` does not measure what ships.** Without AOT, Blazor WebAssembly runs on an IL interpreter, and AOT compilation only happens in `dotnet publish`, so a dev-server run stays interpreted even when the project sets `RunAOTCompilation`. AOT moves the per-frame baseline by a large factor on CPU-heavy code, so an interpreted number and an AOT number are never comparable.
- **Measure a Release publish served over HTTP.** A standalone publish puts the site in the `wwwroot` folder under the publish output (`bin/Release/net10.0/publish/wwwroot`); serving `publish/` itself returns 404 on `/`. Delete the publish folder before republishing, because publish leaves files from earlier publishes in place.
- **No `blazor.boot.json` is not a sign of AOT.** Since .NET 10 every Blazor WebAssembly publish inlines its boot config into `dotnet.js`.
- **Console output is part of the measurement.** On WebAssembly `Console.WriteLine` lands in the browser console, which makes it an easy probe and also a real per-frame cost. Remove or `#if` out per-frame logging before capturing a number.
- **A fixed timestep is required on every platform, browser included; never turn it off.** `Game.IsFixedTimeStep` defaults to `true` on both MonoGame and KNI, and the engine never changes it. Setting it to `false` for smoother per-frame updates in the browser turns one long frame into one large physics and collision step, which lets fast objects tunnel through each other.
- **Browsers are not comparable to each other.** Large Chromium-vs-Firefox frame-time ratios on the same build are normal, so one browser's number is not evidence about the other. Across browsers, compare counts such as `DrawCalls` and `Sprites`, not phase milliseconds.

## Dropped clicks and first-frame stalls

- **A click that "didn't register" is usually a frame that hadn't drawn yet.** Before changing input handling, timestamp the input against the frame: a capture-phase `pointerdown` listener logging `event.timeStamp`, next to a `Console.WriteLine` from game code, shows whether the input arrived during a long frame. Remove the probe before measuring anything else.
- **Keep temporary probes out of `index.html`.** A probe added there survives rebuilds, and `index.html` is the one boot file without a content hash, so the browser can keep serving an old copy. Old instrumentation paired with new code reads as "my fix didn't work."
- **A stall on the first frames after a screen or map load that never recurs is allocation, GC, or warm-up, not a per-frame cost.** Don't chase it with hot-loop optimization. `PerformanceMonitor` does not track allocations; compare `GC.GetTotalAllocatedBytes()` and `GC.CollectionCount(n)` across the transition frame against steady state.

## Startup timing (boot, not per-frame)

`FlatRedBallService.Default.StartupTiming` (a `StartupProfiler`, `src/Diagnostics/StartupProfiler.cs`) times boot and load once — for "why does my game take so long to start," not runtime FPS.

Enable via `EngineInitSettings.ProfileStartup = true` passed to `Initialize`, not on the profiler itself — profiling must be on before `Initialize` opens its first phase, so there's no turning it on afterward.

The report prints itself automatically, once, right after the first screen finishes loading — don't call `GenerateReport()` yourself, and it won't fire again on later screen transitions.

Game code can wrap its own slow `CustomInitialize` work in `BeginPhase`/`EndPhase` to show up in the same report.
