---
name: performance
description: FlatRedBallService.Performance — opt-in rolling FPS/timing/collision stats; StartupProfiler for one-time boot/load timing. Triggers: PerformanceMonitor, GenerateReport, FPS, frame time, DeepCollisionCount, StartupProfiler, ProfileStartup, "why is my game slow", "slow to load".
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

**Landmine:** browsers coarsen their clock as a Spectre mitigation — ~1ms on Firefox/Safari, ~0.1ms on Chrome — and `Stopwatch.Frequency` does not reflect it. Whole-pass totals stay accurate (start/end errors cancel), but any per-phase number smaller than one step reads as 0 or one full step and nothing between. The report warns automatically above 0.5ms.

Set `PlatformLabel` from the host — the engine targets `net10.0` and cannot read `navigator.userAgent` itself.

Every row in the collision report carries a `PartitionStatus`. `Unpartitioned` is the only value worth acting on, and the fix is to set the same `Factory<T>.PartitionAxis` on both sides of the relationship. `NotApplicable` means one side is not a factory, such as a `TileShapes`, a single entity, or a plain `List<T>`, so no axis setting would change it. See the `collision-relationships` skill.

See `src/Diagnostics/FrameProfile.cs` for the underlying per-frame timing struct.

## Startup timing (boot, not per-frame)

`FlatRedBallService.Default.StartupTiming` (a `StartupProfiler`, `src/Diagnostics/StartupProfiler.cs`) times boot and load once — for "why does my game take so long to start," not runtime FPS.

Enable via `EngineInitSettings.ProfileStartup = true` passed to `Initialize`, not on the profiler itself — profiling must be on before `Initialize` opens its first phase, so there's no turning it on afterward.

The report prints itself automatically, once, right after the first screen finishes loading — don't call `GenerateReport()` yourself, and it won't fire again on later screen transitions.

Game code can wrap its own slow `CustomInitialize` work in `BeginPhase`/`EndPhase` to show up in the same report.
