using FlatRedBall2;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace Beefball.Entities;

/// <summary>
/// The soft glow around the neon walls and inside the goal pockets, drawn with Gum gradient shapes:
/// linear-gradient strips along every wall, radial-gradient squares in the corners, and a
/// linear-gradient fill per pocket. One container holds everything so the pieces can be positioned
/// in arena coordinates.
/// </summary>
public class ArenaGlow : Entity
{
    private const float PieceLength = 40f;

    public BeefballConfig Config { get; set; } = null!;

    private readonly RectangleRuntime[] _pocketFill = new RectangleRuntime[2];
    private readonly Color[] _pocketColor = new Color[2];
    private ContainerRuntime _root = null!;

    public override void CustomInitialize()
    {
        Z = -60f;
        _root = new ContainerRuntime();
        Add(_root);

        BuildWalls();
        BuildPockets();
    }

    private void Place(GraphicalUiElement element, float worldX, float worldY)
    {
        // Inside the container: Gum units, Y down.
        element.X = worldX;
        element.Y = -worldY;
        _root.AddChild(element);
    }

    /// <summary>
    /// Each wall gets a linear-gradient strip on both sides, and each corner gets a square with a radial
    /// gradient centered on the corner point that fills the one quadrant the strips leave empty. Nothing
    /// overlaps except where two strips meet inside a corner, and there the strip ends sit under the
    /// crisp wall lines, so there are no visible seams. (Piles of overlapping gradient shapes show faint
    /// streaks from the shape renderer, so overlap is kept to a minimum.)
    /// </summary>
    private void BuildWalls()
    {
        var palette = Config.Palette;
        float depth = Config.Glow.Wall.Width;
        float alpha = Config.Glow.Wall.Alpha * Config.Glow.Strength;

        // Directions pointing along each line away from each of its endpoints, to find the corners.
        var cornerDirections = new Dictionary<(int, int), List<(int Dx, int Dy)>>();
        void NoteEnd(float x, float y, float dx, float dy)
        {
            var key = ((int)MathF.Round(x), (int)MathF.Round(y));
            if (!cornerDirections.TryGetValue(key, out var list))
                cornerDirections[key] = list = new List<(int, int)>();
            list.Add((Math.Sign(dx), Math.Sign(dy)));
        }

        foreach (var (x1, y1, x2, y2) in Arena.NeonLines())
        {
            bool horizontal = Math.Abs(y2 - y1) < 0.001f;
            float length = horizontal ? Math.Abs(x2 - x1) : Math.Abs(y2 - y1);
            int pieces = Math.Max(1, (int)MathF.Round(length / PieceLength));
            float pieceLength = length / pieces;
            NoteEnd(x1, y1, x2 - x1, y2 - y1);
            NoteEnd(x2, y2, x1 - x2, y1 - y2);

            for (int i = 0; i < pieces; i++)
            {
                float mid = (i + 0.5f) / pieces;
                float cx = x1 + (x2 - x1) * mid;
                float cy = y1 + (y2 - y1) * mid;
                var color = Visuals.WallColorAt(palette, cx);

                foreach (int side in new[] { 1, -1 })
                {
                    float offset = side * depth / 2f;
                    var strip = Visuals.CreateLinearGlow(color, alpha, pieceLength, depth, horizontal, side);
                    if (horizontal) Place(strip, cx, cy + offset);
                    else Place(strip, cx + offset, cy);
                }
            }
        }

        foreach (var ((px, py), directions) in cornerDirections)
        {
            if (directions.Count != 2) continue;
            // The empty quadrant lies opposite the two lines.
            int qx = -(directions[0].Dx + directions[1].Dx);
            int qy = -(directions[0].Dy + directions[1].Dy);
            if (qx == 0 || qy == 0) continue; // straight join, no notch
            var color = Visuals.WallColorAt(palette, px);
            var corner = Visuals.CreateCornerGlow(color, alpha, depth, qx, qy);
            Place(corner, px + qx * depth / 2f, py + qy * depth / 2f);
        }
    }

    private void BuildPockets()
    {
        float w = Arena.HalfWidth;
        float d = Arena.PocketDepth;
        float g = Arena.GoalHalfHeight;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _pocketColor[i] = Config.Palette.TeamColor(i + 1);
            // Brightest at the back wall (outer edge), fading toward the mouth.
            var fill = Visuals.CreateLinearGlow(_pocketColor[i], Config.Glow.PocketAlpha, 2f * g, d, horizontal: false,
                side: i == 0 ? 1 : -1);
            _pocketFill[i] = fill;
            Place(fill, side * (w + d / 2f), 0f);
        }
    }

    /// <summary>Brightens a goal pocket. 0 = resting, 1 = full flash. Pocket 0 is left, 1 is right.</summary>
    public void SetPocketPulse(int pocket, float amount)
    {
        Visuals.SetGlowColor(_pocketFill[pocket], _pocketColor[pocket], Config.Glow.PocketAlpha + 0.45f * amount);
    }
}
