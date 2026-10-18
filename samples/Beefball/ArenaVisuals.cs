using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Beefball;

/// <summary>
/// All the non-interactive arena art: floor wash, grid, center marks, glowing walls and goal pockets,
/// plus the goal pocket pulse. Everything is engine shapes.
/// </summary>
public sealed class ArenaVisuals
{
    private const float PocketFillBase = 0.07f;
    private const float PocketHaloBase = 0f;
    private static readonly float[] LineGlowThickness = { 34f, 24f, 15f, 8f };
    private static readonly float[] LineGlowAlpha = { 0.018f, 0.03f, 0.045f, 0.07f };

    private readonly Screen _screen;
    private readonly BeefballConfig _config;

    // Index 0 = left pocket (team 1 defends it), 1 = right pocket (team 2 defends it).
    private readonly AARect[] _pocketFill = new AARect[2];
    private readonly AARect[] _pocketHalo = new AARect[2];
    private readonly Color[] _pocketColor = new Color[2];

    public ArenaVisuals(Screen screen, BeefballConfig config)
    {
        _screen = screen;
        _config = config;
        BuildFloor();
        BuildWalls();
        BuildPockets();
    }

    private Color WallColorAt(float x)
    {
        float t = Math.Clamp((x + Arena.HalfWidth) / (2f * Arena.HalfWidth), 0f, 1f);
        return Color.Lerp(_config.Palette.Team1, _config.Palette.Team2, t);
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

    private void BuildWalls()
    {
        float h = Arena.HalfHeight;
        float w = Arena.HalfWidth;
        float g = Arena.GoalHalfHeight;
        float d = Arena.PocketDepth;
        const float overhang = 2f;

        // Top and bottom run the full width and blend from team 1 to team 2 color.
        AddNeonLine(-w - overhang, h, w + overhang, h, WallColorAt);
        AddNeonLine(-w - overhang, -h, w + overhang, -h, WallColorAt);

        // Left and right walls on either side of the goal mouths.
        foreach (float side in new[] { -1f, 1f })
        {
            var color = WallColorAt(side * w);
            AddNeonLine(side * w, g - overhang, side * w, h + overhang, _ => color);
            AddNeonLine(side * w, -h - overhang, side * w, -g + overhang, _ => color);
            // Pocket roof, floor and back wall in the color of the side the pocket sits on.
            AddNeonLine(side * w, g, side * (w + d), g, _ => color);
            AddNeonLine(side * w, -g, side * (w + d), -g, _ => color);
            AddNeonLine(side * (w + d), -g - overhang, side * (w + d), g + overhang, _ => color);
        }
    }

    /// <summary>
    /// A crisp thin line with two wider, fainter layers behind it, split into short pieces so the color
    /// can change along its length. Vertical lines are given by equal x values.
    /// </summary>
    private void AddNeonLine(float x1, float y1, float x2, float y2, Func<float, Color> colorAtX)
    {
        bool horizontal = Math.Abs(y2 - y1) < 0.001f;
        float length = horizontal ? Math.Abs(x2 - x1) : Math.Abs(y2 - y1);
        int pieces = Math.Max(1, (int)Math.Ceiling(length / 20f));
        float glow = _config.Glow.Strength;

        for (int i = 0; i < pieces; i++)
        {
            float mid = (i + 0.5f) / pieces;
            float cx = x1 + (x2 - x1) * mid;
            float cy = y1 + (y2 - y1) * mid;
            float pieceLength = length / pieces;
            var color = colorAtX(cx);

            // Soft falloff: a few progressively wider, fainter layers, then the crisp core.
            for (int layer = 0; layer < LineGlowThickness.Length; layer++)
            {
                float alpha = LineGlowAlpha[layer] * glow;
                AddLinePiece(cx, cy, pieceLength, LineGlowThickness[layer], horizontal, Visuals.WithAlpha(color, alpha), -60f + layer);
            }
            AddLinePiece(cx, cy, pieceLength + 1f, 3f, horizontal, Visuals.WithAlpha(color, 0.92f), -55f);
        }
    }

    private void AddLinePiece(float cx, float cy, float length, float thickness, bool horizontal, Color color, float z)
    {
        if (horizontal) AddRect(cx, cy, length, thickness, color, z);
        else AddRect(cx, cy, thickness, length, color, z);
    }

    private void BuildPockets()
    {
        float w = Arena.HalfWidth;
        float d = Arena.PocketDepth;
        float g = Arena.GoalHalfHeight;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var color = _config.Palette.TeamColor(i + 1);
            _pocketColor[i] = color;
            _pocketHalo[i] = AddRect(side * (w + d / 2f - 8f), 0f, d + 40f, 2f * g + 24f,
                Visuals.WithAlpha(color, PocketHaloBase * _config.Glow.Strength), -86f);
            _pocketFill[i] = AddRect(side * (w + d / 2f), 0f, d, 2f * g,
                Visuals.WithAlpha(color, PocketFillBase), -85f);
        }
    }

    /// <summary>Brightens a goal pocket. 0 = resting, 1 = full flash. Pocket 0 is left, 1 is right.</summary>
    public void SetPocketPulse(int pocket, float amount)
    {
        float glow = _config.Glow.Strength;
        _pocketFill[pocket].Color = Visuals.WithAlpha(_pocketColor[pocket], PocketFillBase + 0.38f * amount);
        _pocketHalo[pocket].Color = Visuals.WithAlpha(_pocketColor[pocket], (PocketHaloBase + 0.08f * amount) * glow);
    }
}
