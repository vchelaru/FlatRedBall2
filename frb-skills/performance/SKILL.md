---
name: performance
description: FlatRedBallService.Performance — opt-in rolling FPS/timing/collision stats. Triggers: PerformanceMonitor, GenerateReport, FPS, frame time, DeepCollisionCount, "why is my game slow".
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
var worst = perf.GetCollisionReport();      // ordered most-expensive first, with a PartitionStatus each
```

`GenerateReport()` only builds a string — it does no I/O itself, so printing/logging/writing it is on the caller.

## Timer resolution (web)

`GenerateReport()` opens with a `Platform:` line and the measured clock step (`TimerResolutionMs`, probed once via `ProfileClock.MeasureResolutionMs`).

**Landmine:** browsers coarsen their clock as a Spectre mitigation — ~1ms on Firefox/Safari, ~0.1ms on Chrome — and `Stopwatch.Frequency` does not reflect it. Whole-pass totals stay accurate (start/end errors cancel), but any per-phase number smaller than one step reads as 0 or one full step and nothing between. The report warns automatically above 0.5ms.

Set `PlatformLabel` from the host — the engine targets `net10.0` and cannot read `navigator.userAgent` itself.

Each row carries a `PartitionStatus`: only `Unpartitioned` is worth acting on (set a matching `Factory<T>.PartitionAxis` on both sides). `NotApplicable` means a non-factory side — `TileShapes`, a single entity, a plain `List<T>` — where no axis setting applies; see the `collision-relationships` skill.

See `src/Diagnostics/FrameProfile.cs` for the underlying per-frame timing struct.
