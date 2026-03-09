using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FlatRedBall2;
using NVec2 = System.Numerics.Vector2;

namespace SampleProject1.Screens;

/// <summary>
/// Demonstrates all <see cref="Overlay"/> draw methods interactively.
/// Press SPACE to cycle through states; move the mouse to control position/size/direction.
/// </summary>
public class OverlayDemoScreen : Screen
{
    private enum State { Circle, Rectangle, Line, Arrow, Polygon, Sprite, Text }

    private State _state = State.Circle;
    private Label _hintLabel = null!;
    private Texture2D _spriteTexture = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(18, 18, 28);

        _spriteTexture = CreateCheckerTexture(Engine.GraphicsDevice);

        var hud = new Panel();
        hud.Dock(Dock.Fill);

        _hintLabel = new Label();
        _hintLabel.Anchor(Anchor.TopLeft);
        _hintLabel.X = 10;
        _hintLabel.Y = 10;
        hud.AddChild(_hintLabel);

        Add(hud);
        RefreshHint();
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb     = Engine.Input.Keyboard;
        var cursor = Engine.Input.Cursor;

        if (kb.WasKeyPressed(Keys.Space))
        {
            _state = (State)(((int)_state + 1) % 7);
            RefreshHint();
        }

        float mx = cursor.WorldPosition.X;
        float my = cursor.WorldPosition.Y;

        // Small crosshair at origin so the user sees where Line/Arrow start from.
        Engine.Overlay.Line(-15f, 0f, 15f, 0f, Color.DimGray);
        Engine.Overlay.Line(0f, -15f, 0f, 15f, Color.DimGray);

        switch (_state)
        {
            case State.Circle:
                // Mouse = center; radius is fixed so the shape clearly follows the cursor.
                Engine.Overlay.Circle(mx, my, 60f, Color.LimeGreen);
                break;

            case State.Rectangle:
                Engine.Overlay.Rectangle(mx, my, 140f, 80f, Color.Orange);
                break;

            case State.Line:
                Engine.Overlay.Line(0f, 0f, mx, my, Color.Yellow);
                var originDot = Overlay.Circle(0f, 0f, 5f, Color.Yellow);
                originDot.IsFilled = true;
                break;

            case State.Arrow:
                // Mouse = tip — the filled triangular head rotates as you move.
                Engine.Overlay.Arrow(0f, 0f, mx, my, Color.Tomato);
                var arrowBase = Overlay.Circle(0f, 0f, 5f, Color.Tomato);
                arrowBase.IsFilled = true;
                break;

            case State.Polygon:
                // Equilateral triangle, mouse = center.
                var pts = new[]
                {
                    new NVec2(  0f,  55f),
                    new NVec2( 48f, -28f),
                    new NVec2(-48f, -28f),
                };
                Engine.Overlay.Polygon(mx, my, pts, Color.MediumPurple);
                break;

            case State.Sprite:
                Engine.Overlay.Sprite(_spriteTexture, mx, my);
                break;

            case State.Text:
                var label = Overlay.Text("Overlay.Text!", mx, my);
                Engine.Overlay.TextBackground(label, new Color(0, 20, 80, 210));
                break;
        }
    }

    public override void CustomDestroy()
    {
        _spriteTexture.Dispose();
    }

    private void RefreshHint()
    {
        _hintLabel.Text = $"State: {_state}  |  SPACE = next  |  Move mouse to interact";
    }

    // Creates a simple 32×32 cyan/white checkerboard so the sprite is visually obvious.
    private static Texture2D CreateCheckerTexture(GraphicsDevice gd)
    {
        const int Size = 32;
        var tex  = new Texture2D(gd, Size, Size);
        var data = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool light = ((x / 8) + (y / 8)) % 2 == 0;
            data[y * Size + x] = light ? Color.Cyan : new Color(0, 160, 200);
        }
        tex.SetData(data);
        return tex;
    }
}
