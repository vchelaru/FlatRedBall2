---
name: tile-node-network
description: "Tile Node Network in FlatRedBall2. Use when working with A* pathfinding, TileNodeNetwork, TileNode, enemy navigation, or grid-based pathfinding. Trigger on any pathfinding or enemy-follows-player question."
---

# Tile Node Network (A* Pathfinding)

## Key Types

- `FlatRedBall2.AI.TileNodeNetwork` — the grid; builds and queries the A* graph
- `FlatRedBall2.AI.TileNode` — one cell; has `Position`, `Tag`, `Neighbors`
- `FlatRedBall2.AI.DirectionalType` — `Four` (cardinal only) or `Eight` (+ diagonals)

## Standard Setup

The standard FlatRedBall2 tile size is **16 units**. Node origins are offset by half a tile from the `TileShapes` corner, so node centers fall at 8, 24, 40, … (i.e. `tileCollection.X + 8`, `tileCollection.Y + 8`).

```csharp
var network = new TileNodeNetwork(
    xOrigin:         tilesOriginX + 8f,   // center of first column
    yOrigin:         tilesOriginY + 8f,   // center of bottom row
    gridSpacing:     16f,
    xCount:          gridCols,
    yCount:          gridRows,
    directionalType: DirectionalType.Eight);

network.FillCompletely();

// Remove nodes where walls are (call after all wall tiles are placed)
foreach (var (col, row) in wallCells)
    network.RemoveAt(col, row);

// For 8-way: prevent squeezing diagonally through wall corners
network.EliminateCutCorners();
```

## Getting a Path

```csharp
// Allocating — returns a new List<Vector2>
List<Vector2> path = network.GetPath(startNode, endNode);

// Non-allocating — clears and fills an existing list; returns true if path found
bool found = network.GetPath(startNode, endNode, _path);
```

The **start node is excluded**; the end node is the last entry. Returns empty / false if no path exists.

## Finding Nodes

```csharp
TileNode? node = network.NodeAt(worldX, worldY);            // exact cell, null if empty
TileNode? node = network.GetClosestNode(worldX, worldY);    // nearest occupied node
```

Use `GetClosestNode` to find the start/end nodes from entity world positions.

## TileNode.Tag

Attach game data to nodes (terrain type, waypoint markers, etc.):

```csharp
node.Tag = MyTerrainType.Mud;
```

## Typical Enemy AI Pattern

Refresh the path on a timer (not every frame) to limit A* cost:

```csharp
private readonly List<Vector2> _path = new();
private int   _waypointIndex;
private float _pathTimer;

void Activity(FrameTime time)
{
    _pathTimer -= time.DeltaSeconds;
    if (_pathTimer <= 0f)
    {
        var start = _network.GetClosestNode(X, Y);
        var end   = _network.GetClosestNode(_target.X, _target.Y);
        if (start != null && end != null)
            _network.GetPath(start, end, _path);
        _waypointIndex = 0;
        _pathTimer = 1f;   // refresh once per second
    }

    // Advance past reached waypoints
    while (_waypointIndex < _path.Count)
    {
        float dx = _path[_waypointIndex].X - X;
        float dy = _path[_waypointIndex].Y - Y;
        if (dx * dx + dy * dy <= WaypointRadius * WaypointRadius)
            _waypointIndex++;
        else break;
    }

    var dest = _waypointIndex < _path.Count
        ? _path[_waypointIndex]
        : new Vector2(_target.X, _target.Y);   // path exhausted — steer directly

    // Apply velocity toward dest …
}
```

## Gotchas

- **A* state is stored on nodes** — `GetPath` resets all node state at the start of each call. Concurrent calls on the same network (same frame, multiple enemies) are fine because the game loop is single-threaded and each call resets before using.
- **Diagonal cost is √2 × gridSpacing** naturally — node positions are used for distance, so diagonals cost more without any extra configuration.
- **`RemoveAt` unlinks neighbors** — removing a node severs all its connections. You do not need to call `EliminateCutCorners` again after individual removals.
