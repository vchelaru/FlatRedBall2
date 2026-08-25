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
var worst = perf.GetCollisionReport();      // ordered most-expensive first, flags unpartitioned relationships
```

`GenerateReport()` only builds a string — it does no I/O itself, so printing/logging/writing it is on the caller.

See `src/Diagnostics/FrameProfile.cs` for the underlying per-frame timing struct, and `Factory<T>.PartitionAxis` for what "unpartitioned" in the collision report means.
