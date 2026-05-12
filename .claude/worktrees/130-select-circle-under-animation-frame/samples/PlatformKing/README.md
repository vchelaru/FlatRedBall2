# PlatformKing

A FlatRedBall2 sample demonstrating a 2D platformer: variable-height jumps, double jump, one-way cloud platforms, ladders, swimming zones, patrolling enemies, destructible boxes, and two interconnected Tiled levels. See [`design.md`](./design.md) for the full design doc.

## Run

```
dotnet run --project samples/PlatformKing/PlatformKing.csproj
```

## Controls

| Input | Action |
|---|---|
| A / Left Arrow | Move left |
| D / Right Arrow | Move right |
| Space | Jump (hold for higher jump; double jump in air) |
| Up Arrow | Grab ladder / climb up |
| Down Arrow | Climb down / drop through cloud |
| Up / Down | Swim vertically (in water) |
| Escape | Quit |
