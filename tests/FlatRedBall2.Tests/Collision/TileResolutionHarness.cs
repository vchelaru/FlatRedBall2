using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Tests.Collision;

// Shared helpers for the TileResolution* characterization tests. These tests pin how an actor
// is pushed out of a TileShapes today, so the harness deliberately measures overlap with its own
// geometry (not the dispatcher) to keep "ends outside all tiles" independent of the code under test.

internal enum ActorKind { Rect, Circle, PolyRect, Diamond, Custom }

/// <summary>An actor shape description: kind plus half extents (Circle: HalfW is the radius).</summary>
internal sealed record ActorSpec(ActorKind Kind, float HalfW, float HalfH)
{
    public static ActorSpec Rect(float halfW, float halfH) => new(ActorKind.Rect, halfW, halfH);
    public static ActorSpec Square(float half) => new(ActorKind.Rect, half, half);
    public static ActorSpec Circle(float radius) => new(ActorKind.Circle, radius, radius);
    public static ActorSpec PolyRect(float halfW, float halfH) => new(ActorKind.PolyRect, halfW, halfH);
    /// <summary>A square rotated 45 degrees; <paramref name="half"/> is the half-diagonal.</summary>
    public static ActorSpec Diamond(float half) => new(ActorKind.Diamond, half, half);

    /// <summary>Any convex polygon given in local coordinates around the actor center; half extents are taken from its bounds.</summary>
    public static ActorSpec Convex(string name, params Vector2[] localPoints) =>
        new(ActorKind.Custom, localPoints.Max(p => MathF.Abs(p.X)), localPoints.Max(p => MathF.Abs(p.Y))) { LocalPoints = localPoints, Name = name };

    public Vector2[] LocalPoints { get; init; } = Array.Empty<Vector2>();
    public string Name { get; init; } = "";

    public IAttachable CreateShape() => Kind switch
    {
        ActorKind.Rect => new AARect { Width = HalfW * 2f, Height = HalfH * 2f },
        ActorKind.Circle => new Circle { Radius = HalfW },
        ActorKind.PolyRect => Polygon.CreateRectangle(HalfW * 2f, HalfH * 2f),
        ActorKind.Diamond => Polygon.FromPoints(new[]
        {
            new Vector2(0f, -HalfH), new Vector2(HalfW, 0f), new Vector2(0f, HalfH), new Vector2(-HalfW, 0f),
        }),
        ActorKind.Custom => Polygon.FromPoints(LocalPoints),
        _ => throw new ArgumentOutOfRangeException(),
    };

    /// <summary>World-space polygon corners when centered at (x, y); empty for circles.</summary>
    public Vector2[] Corners(float x, float y) => Kind switch
    {
        ActorKind.Rect or ActorKind.PolyRect => new[]
        {
            new Vector2(x - HalfW, y - HalfH), new Vector2(x + HalfW, y - HalfH),
            new Vector2(x + HalfW, y + HalfH), new Vector2(x - HalfW, y + HalfH),
        },
        ActorKind.Diamond => new[]
        {
            new Vector2(x, y - HalfH), new Vector2(x + HalfW, y), new Vector2(x, y + HalfH), new Vector2(x - HalfW, y),
        },
        ActorKind.Custom => LocalPoints.Select(q => new Vector2(x + q.X, y + q.Y)).ToArray(),
        _ => Array.Empty<Vector2>(),
    };

    public override string ToString() => Kind == ActorKind.Circle ? $"Circle r={HalfW}" : Kind == ActorKind.Custom ? Name : $"{Kind} {HalfW}x{HalfH}";
}

internal static class TileWorld
{
    public const float Grid = 16f;
    public const float Eps = 1e-2f;

    /// <summary>
    /// Builds tiles from ASCII rows. rows[0] is the top row; the last row is cell row 0. Cell (0, 0)'s
    /// bottom-left corner is the world origin, so cell (c, r) spans x in [16c, 16c+16], y in [16r, 16r+16].
    /// '#' is a tile, anything else is empty.
    /// </summary>
    public static TileShapes Map(params string[] rows) => MapAt(0f, 0f, rows);

    /// <summary>Same as <see cref="Map"/> but with the TileShapes origin moved to (originX, originY).</summary>
    public static TileShapes MapAt(float originX, float originY, params string[] rows)
    {
        var tiles = new TileShapes { GridSize = Grid, X = originX, Y = originY };
        int height = rows.Length;
        for (int i = 0; i < height; i++)
            for (int c = 0; c < rows[i].Length; c++)
                if (rows[i][c] == '#')
                    tiles.AddTileAtCell(c, height - 1 - i);
        return tiles;
    }

    public static Vector2 Center(int col, int row) => new((col + 0.5f) * Grid, (row + 0.5f) * Grid);

    public static bool HasTile(TileShapes tiles, int col, int row) => tiles.GetTileAtCell(col, row) != null;

    public static (int col, int row) CellOf(float x, float y) =>
        ((int)MathF.Floor(x / Grid), (int)MathF.Floor(y / Grid));

    public static (Entity actor, CollisionRelationship<Entity, TileShapes> rel) Actor(
        ActorSpec spec, TileShapes tiles, float x, float y)
    {
        var actor = new Entity();
        actor.Add(spec.CreateShape());
        actor.X = x;
        actor.Y = y;
        var rel = new CollisionRelationship<Entity, TileShapes>(new[] { actor }, new[] { tiles });
        rel.MoveFirstOnCollision();
        return (actor, rel);
    }

    /// <summary>Runs one collision pass and returns the actor's final center.</summary>
    public static Vector2 ResolveOnce(ActorSpec spec, TileShapes tiles, float x, float y)
    {
        var (actor, rel) = Actor(spec, tiles, x, y);
        rel.RunCollisions();
        return new Vector2(actor.X, actor.Y);
    }

    /// <summary>Runs up to <paramref name="maxPasses"/> passes and returns the center after each (index 0 = after pass 1).</summary>
    public static List<Vector2> ResolvePasses(ActorSpec spec, TileShapes tiles, float x, float y, int maxPasses)
    {
        var (actor, rel) = Actor(spec, tiles, x, y);
        var result = new List<Vector2>();
        for (int i = 0; i < maxPasses; i++)
        {
            rel.RunCollisions();
            result.Add(new Vector2(actor.X, actor.Y));
        }
        return result;
    }

    /// <summary>Runs passes until the actor stops moving (or <paramref name="maxPasses"/>) and returns the final center.</summary>
    public static Vector2 ResolveToRest(ActorSpec spec, TileShapes tiles, float x, float y, int maxPasses = 8)
    {
        var (actor, rel) = Actor(spec, tiles, x, y);
        for (int i = 0; i < maxPasses; i++)
        {
            var before = new Vector2(actor.X, actor.Y);
            rel.RunCollisions();
            if (new Vector2(actor.X, actor.Y) == before) break;
        }
        return new Vector2(actor.X, actor.Y);
    }

    // ── Independent geometry ────────────────────────────────────────────────────────────────

    /// <summary>True when the actor at (x, y) overlaps any full-cell tile by more than <see cref="Eps"/> on every axis.</summary>
    public static bool Penetrates(ActorSpec spec, float x, float y, TileShapes tiles)
    {
        // Only tiles within reach matter.
        float reach = MathF.Max(spec.HalfW, spec.HalfH) + Grid;
        foreach (var tile in tiles.Rectangles)
        {
            if (MathF.Abs(tile.AbsoluteX - x) > reach || MathF.Abs(tile.AbsoluteY - y) > reach) continue;
            if (Penetrates(spec, x, y, tile.AbsoluteX - tile.Width / 2f, tile.AbsoluteX + tile.Width / 2f,
                    tile.AbsoluteY - tile.Height / 2f, tile.AbsoluteY + tile.Height / 2f))
                return true;
        }
        return false;
    }

    public static bool Penetrates(ActorSpec spec, float x, float y, float left, float right, float bottom, float top)
    {
        if (spec.Kind == ActorKind.Circle)
        {
            float px = System.Math.Clamp(x, left, right), py = System.Math.Clamp(y, bottom, top);
            float dx = x - px, dy = y - py;
            if (dx == 0f && dy == 0f) return true;
            return MathF.Sqrt(dx * dx + dy * dy) < spec.HalfW - Eps;
        }

        var poly = spec.Corners(x, y);
        // SAT: axes are the rect's X and Y plus the polygon edge normals. Penetrating when overlap > Eps on every axis.
        var axes = new List<Vector2> { new(1f, 0f), new(0f, 1f) };
        for (int i = 0; i < poly.Length; i++)
        {
            var e = poly[(i + 1) % poly.Length] - poly[i];
            var n = Vector2.Normalize(new Vector2(-e.Y, e.X));
            axes.Add(n);
        }
        var rectPts = new[] { new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top) };
        foreach (var axis in axes)
        {
            Project(poly, axis, out float pMin, out float pMax);
            Project(rectPts, axis, out float rMin, out float rMax);
            float overlap = MathF.Min(pMax, rMax) - MathF.Max(pMin, rMin);
            if (overlap <= Eps) return false;
        }
        return true;
    }

    private static void Project(Vector2[] pts, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue; max = float.MinValue;
        foreach (var p in pts)
        {
            float d = Vector2.Dot(p, axis);
            if (d < min) min = d;
            if (d > max) max = d;
        }
    }

    /// <summary>True when the straight segment a→b passes only through empty cells (sampled every 1 unit).</summary>
    public static bool SegmentIsClear(TileShapes tiles, Vector2 a, Vector2 b)
    {
        float len = (b - a).Length();
        int steps = System.Math.Max(1, (int)MathF.Ceiling(len));
        for (int i = 0; i <= steps; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)steps);
            var (c, r) = CellOf(p.X, p.Y);
            if (HasTile(tiles, c, r)) return false;
        }
        return true;
    }

    /// <summary>Start positions on a regular grid across [minX..maxX]x[minY..maxY], offset so they never sit on a tile seam.</summary>
    public static IEnumerable<Vector2> ScanPositions(float minX, float maxX, float minY, float maxY, float step)
    {
        for (float y = minY + 0.37f; y <= maxY; y += step)
            for (float x = minX + 0.61f; x <= maxX; x += step)
                yield return new Vector2(x, y);
    }
}

/// <summary>A moving actor integrated frame by frame against tiles (PhysicsUpdate then RunCollisions, as the screen does).</summary>
internal sealed class TileSim
{
    public const float Dt = 1f / 60f;
    public Entity Actor { get; }
    public TileShapes Tiles { get; }
    private readonly CollisionRelationship<Entity, TileShapes> _rel;
    private float _time;

    /// <summary>The relationship driving the actor, for tests that configure OneWayDirection and the like.</summary>
    public CollisionRelationship<Entity, TileShapes> Relationship => _rel;

    public TileSim(ActorSpec spec, TileShapes tiles, float x, float y, bool zeroVelocityIntoSurface = true)
    {
        Tiles = tiles;
        Actor = new Entity();
        Actor.Add(spec.CreateShape());
        Actor.X = x;
        Actor.Y = y;
        _rel = new CollisionRelationship<Entity, TileShapes>(new[] { Actor }, new[] { tiles });
        if (zeroVelocityIntoSurface) _rel.BounceFirstOnCollision(elasticity: 0f);
        else _rel.MoveFirstOnCollision();
    }

    public Vector2 Position => new(Actor.X, Actor.Y);

    public void Step()
    {
        _time += Dt;
        Actor.PhysicsUpdate(new FrameTime(TimeSpan.FromSeconds(Dt), TimeSpan.FromSeconds(Dt), TimeSpan.FromSeconds(_time), TimeSpan.FromSeconds(_time)));
        _rel.RunCollisions();
    }

    public void Step(int frames)
    {
        for (int i = 0; i < frames; i++) Step();
    }
}
