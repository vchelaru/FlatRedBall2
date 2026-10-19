using Beefball.Entities;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RenderingLibrary.Graphics;
using Layer = FlatRedBall2.Rendering.Layer;

namespace Beefball.Screens;

public class TitleScreen : Screen
{
    private BeefballConfig _config = null!;
    private Factory<GlowOrb> _orbs = null!;
    private TextRuntime _prompt = null!;
    private float _time;

    public override void CustomInitialize()
    {
        _config = BeefballConfig.Load();
        var palette = _config.Palette;
        Camera.BackgroundColor = palette.Background;

        _orbs = new Factory<GlowOrb>(this);
        BuildDecor();

        var hud = new Layer("UI");
        Layers.Add(hud);

        float cx = Arena.ResolutionWidth / 2f;

        // Two-tone title: the halves sit either side of the center line.
        Add(UiText.Make("BEEF", 132, palette.Team1, cx - 4f, 170f, HorizontalAlignment.Right), hud);
        Add(UiText.Make("BALL", 132, palette.Team2, cx + 4f, 170f, HorizontalAlignment.Left), hud);
        Add(UiText.Make(UiText.Spaced("AIR HOCKEY FOR TWO"), 20, Visuals.WithAlpha(palette.Text, 0.6f), cx, 254f), hud);

        _prompt = UiText.Make("Press Enter to start", 30, palette.Text, cx, 520f);
        Add(_prompt, hud);

        AddControls(hud, cx - 230f, "PLAYER 1", palette.Team1, "W A S D  move    B  dash", "Gamepad 1: left stick, A");
        AddControls(hud, cx + 230f, "PLAYER 2", palette.Team2, "Arrows  move    Right Shift  dash", "Gamepad 2: left stick, A");
        Add(UiText.Make("Esc  quit", 16, Visuals.WithAlpha(palette.Text, 0.4f), cx, 704f), hud);

        WatchContentDirectory("Content", relativePath =>
        {
            if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                RestartScreen();
        });
    }

    private void AddControls(Layer layer, float x, string heading, Color color, string line1, string line2)
    {
        var text = _config.Palette.Text;
        Add(UiText.Make(UiText.Spaced(heading), 18, color, x, 600f), layer);
        Add(UiText.Make(line1, 18, Visuals.WithAlpha(text, 0.8f), x, 632f), layer);
        Add(UiText.Make(line2, 16, Visuals.WithAlpha(text, 0.5f), x, 658f), layer);
    }

    /// <summary>Two paddles and a puck between the title and the prompt, with the same gradient glows as the game.</summary>
    private void BuildDecor()
    {
        var palette = _config.Palette;
        const float y = -30f;
        AddOrb(-175f, y, 30f, palette.Team1, palette.PlayerBody, withBody: true);
        AddOrb(175f, y, 30f, palette.Team2, palette.PlayerBody, withBody: true);
        AddOrb(0f, y, 8f, palette.Puck, palette.PuckCore, withBody: false);

        // A faint dashed path hinting at the puck's journey.
        for (int i = 0; i < 9; i++)
        {
            float x = -120f + i * 30f;
            float t = i / 8f;
            Add(new Circle
            {
                X = x, Y = y, Radius = 2f, IsFilled = true, IsVisible = true,
                Color = Visuals.WithAlpha(Color.Lerp(palette.Team1, palette.Team2, t), 0.35f), Z = -5f,
            });
        }
    }

    private void AddOrb(float x, float y, float radius, Color color, Color inner, bool withBody)
    {
        _orbs.Create(o =>
        {
            o.GlowColor = color;
            o.BodyRadius = radius;
            o.Config = withBody ? _config.Glow.Player : _config.Glow.Puck;
            o.Strength = _config.Glow.Strength;
        }).Position = new System.Numerics.Vector2(x, y);

        if (withBody)
        {
            Add(new Circle { X = x, Y = y, Radius = radius, IsFilled = true, IsVisible = true, Color = inner, OutlineThickness = 0f });
            Add(new Circle { X = x, Y = y, Radius = radius, IsFilled = false, IsVisible = true, Color = color, OutlineThickness = 4f, Z = 0.1f });
            Add(new Circle { X = x, Y = y, Radius = 3.5f, IsFilled = true, IsVisible = true, Color = Color.White, Z = 0.2f });
        }
        else
        {
            Add(new Circle { X = x, Y = y, Radius = radius, IsFilled = true, IsVisible = true, Color = color, OutlineThickness = 0f });
            Add(new Circle { X = x, Y = y, Radius = radius * 0.45f, IsFilled = true, IsVisible = true, Color = inner, Z = 0.1f });
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        _time += time.DeltaSeconds;
        _prompt.Alpha = (int)(150f + 105f * (0.5f + 0.5f * MathF.Sin(_time * 3f)));

        var keyboard = Engine.Input.Keyboard;
        if (keyboard.WasKeyPressed(Keys.Enter))
            MoveToScreen<GameScreen>();
        else if (keyboard.WasKeyPressed(Keys.Escape))
            Engine.Game.Exit();
    }
}
