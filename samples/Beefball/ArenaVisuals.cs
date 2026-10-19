using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Beefball;

/// <summary>
/// The crisp arena art: floor wash, grid, center marks and the thin neon wall lines, all engine
/// shapes. The glow around the walls is the gradient entity ArenaGlow.
/// </summary>
public sealed class ArenaVisuals
{

    private readonly Screen _screen;
    private readonly BeefballConfig _config;

    public ArenaVisuals(Screen screen, BeefballConfig config)
    {
        _screen = screen;
        _config = config;
        BuildFloor();
        BuildWalls();
    }

    private AARect AddRect(float x, float y, float w, float h, Color color, float z)
    {
        var rect = new AARect
        {
            X = x, Y = y, Width = w, Height = h, Z = z,
            IsFilled = true, IsVisible = true, Color = color, OutlineThickness = 0f,
        };
        _screen.Add(rect);
        return rect;
    }

    private void BuildFloor()
    {
        var palette = _config.Palette;
        float half = Arena.HalfWidth;
        float full = Arena.HalfHeight * 2f;

        // Barely-there team wash on each half.
        AddRect(-half / 2f, 0f, half, full, Visuals.WithAlpha(palette.Team1, 0.035f), -100f);
        AddRect(half / 2f, 0f, half, full, Visuals.WithAlpha(palette.Team2, 0.035f), -100f);

        // Very faint grid.
        var gridColor = Visuals.WithAlpha(Color.White, 0.022f);
        for (float x = -350f; x <= 350f; x += 50f)
            if (x != 0f) AddRect(x, 0f, 1f, full, gridColor, -95f);
        for (float y = -250f; y <= 250f; y += 50f)
            AddRect(0f, y, 2f * half, 1f, gridColor, -95f);

        // Center line and circle.
        var markColor = Visuals.WithAlpha(Color.White, 0.14f);
        AddRect(0f, 0f, 2f, full, markColor, -90f);
        _screen.Add(new Circle
        {
            Radius = 72f, IsFilled = false, IsVisible = true,
            OutlineThickness = 2f, Color = markColor, Z = -90f,
        });
        _screen.Add(new Circle
        {
            Radius = 4f, IsFilled = true, IsVisible = true, Color = markColor, Z = -90f,
        });
    }

    /// <summary>The crisp wall lines. Their glow is the separate gradient entity <see cref="Entities.ArenaGlow"/>.</summary>
    private void BuildWalls()
    {
        foreach (var (x1, y1, x2, y2) in Arena.NeonLines())
            AddNeonLine(x1, y1, x2, y2);
    }

    /// <summary>
    /// A crisp thin line, split into short pieces so the color can change along its length. Both ends
    /// overhang slightly so corners close.
    /// </summary>
    private void AddNeonLine(float x1, float y1, float x2, float y2)
    {
        const float overhang = 1.5f;
        bool horizontal = Math.Abs(y2 - y1) < 0.001f;
        if (horizontal)
        {
            x1 -= Math.Sign(x2 - x1) * overhang;
            x2 += Math.Sign(x2 - x1) * overhang;
        }
        else
        {
            y1 -= Math.Sign(y2 - y1) * overhang;
            y2 += Math.Sign(y2 - y1) * overhang;
        }

        float length = horizontal ? Math.Abs(x2 - x1) : Math.Abs(y2 - y1);
        int pieces = Math.Max(1, (int)Math.Ceiling(length / 20f));
        for (int i = 0; i < pieces; i++)
        {
            float mid = (i + 0.5f) / pieces;
            float cx = x1 + (x2 - x1) * mid;
            float cy = y1 + (y2 - y1) * mid;
            var color = Visuals.WallColorAt(_config.Palette, cx);
            AddLinePiece(cx, cy, length / pieces + 1f, 3f, horizontal, Visuals.WithAlpha(color, 0.92f), -55f);
        }
    }

    private void AddLinePiece(float cx, float cy, float length, float thickness, bool horizontal, Color color, float z)
    {
        if (horizontal) AddRect(cx, cy, length, thickness, color, z);
        else AddRect(cx, cy, thickness, length, color, z);
    }
}
