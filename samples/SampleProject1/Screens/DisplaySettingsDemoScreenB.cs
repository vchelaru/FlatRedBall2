using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Screens;

/// <summary>
/// Display settings demo — Screen B.
/// Zoom=2, IncreaseVisibleArea, no fixed aspect ratio.
/// Everything appears 2× bigger than Screen A.
/// Resize the window: a larger window reveals more world; no black bars.
/// </summary>
public class DisplaySettingsDemoScreenB : Screen
{
    public override DisplaySettings PreferredDisplaySettings => new DisplaySettings
    {
        Zoom = 2f,
        ResizeMode = ResizeMode.IncreaseVisibleArea,
        AllowUserResizing = true,
    };

    private Label _infoLabel = null!;
    private Texture2D _demoTexture = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 35, 25);

        _demoTexture = CreateSmileyTexture(Engine.GraphicsDevice);
        AddDemoSprites();

        var hud = new Panel();
        hud.Dock(Dock.Fill);

        var stack = new StackPanel();
        stack.Spacing = 12;
        stack.Anchor(Anchor.TopLeft);
        stack.X = 16;
        stack.Y = 16;

        var titleLabel = new Label();
        titleLabel.Text = "Screen B — Zoom 2×, IncreaseVisibleArea, No AR lock";
        stack.AddChild(titleLabel);

        var hintLabel = new Label();
        hintLabel.Text = "Resize window → more world visible; no black bars  |  F11: toggle fullscreen";
        stack.AddChild(hintLabel);

        _infoLabel = new Label();
        stack.AddChild(_infoLabel);

        hud.AddChild(stack);

        var btnA = new Button();
        btnA.Text = "← Screen A  (Zoom 1×, 16:9 locked, StretchVisibleArea)";
        btnA.Anchor(Anchor.BottomLeft);
        btnA.X = 16;
        btnA.Y = -16;
        btnA.Click += (_, _) => GoToScreenA();
        hud.AddChild(btnA);

        var btnC = new Button();
        btnC.Text = "→ Screen C  (Zoom 3×, 480×320, fixed 1440×960 window)";
        btnC.Anchor(Anchor.BottomRight);
        btnC.X = -16;
        btnC.Y = -16;
        btnC.Click += (_, _) => GoToScreenC();
        hud.AddChild(btnC);

        Add(hud);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.F11))
            ToggleFullscreen();

        var mode = Engine.DisplaySettings.WindowMode == WindowMode.FullscreenBorderless ? "Fullscreen" : "Windowed";
        _infoLabel.Text = $"Camera: TargetWidth={Camera.TargetWidth}  TargetHeight={Camera.TargetHeight}  Zoom={Camera.Zoom:F1}  " +
                          $"Visible world: {Camera.TargetWidth / Camera.Zoom:F0}×{Camera.TargetHeight / Camera.Zoom:F0}  [{mode}]";

        DrawWorldMarkers();
    }

    private void ToggleFullscreen()
    {
        var newMode = Engine.DisplaySettings.WindowMode == WindowMode.Windowed
            ? WindowMode.FullscreenBorderless
            : WindowMode.Windowed;
        Engine.ApplyWindowSettings(new DisplaySettings { WindowMode = newMode });
    }

    private void AddDemoSprites()
    {
        var center = new Sprite { Texture = _demoTexture };
        center.TextureScale = null;
        center.Width = 64;
        center.Height = 64;
        Add(center);

        var right = new Sprite { Texture = _demoTexture, X = 160, Y = 80 };
        right.TextureScale = null;
        right.Width = 32;
        right.Height = 32;
        Add(right);

        var left = new Sprite { Texture = _demoTexture, X = -160, Y = -80 };
        left.TextureScale = null;
        left.Width = 32;
        left.Height = 32;
        Add(left);
    }

    private void DrawWorldMarkers()
    {
        float hw = Camera.TargetWidth / Camera.Zoom / 2f;
        float hh = Camera.TargetHeight / Camera.Zoom / 2f;

        Engine.Overlay.Rectangle(0, 0, hw * 2f, hh * 2f, Color.MediumSeaGreen);
        Engine.Overlay.Line(-hw, 0, hw, 0, new Color(40, 100, 60));
        Engine.Overlay.Line(0, -hh, 0, hh, new Color(40, 100, 60));
    }

    public override void CustomDestroy()
    {
        _demoTexture?.Dispose();
    }

    private static Texture2D CreateSmileyTexture(GraphicsDevice gd)
    {
        var Y = new Color(255, 200, 0);
        var E = new Color(50, 30, 10);
        var _ = Color.Transparent;

        var rows = new[]
        {
            new[]{ _,_,_,_, Y,Y,Y,Y,Y,Y,Y,Y, _,_,_,_ },
            new[]{ _,_,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_,_ },
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },
            new[]{ _, Y,Y,Y,Y,Y,Y,_,_,Y,Y,Y,Y,Y,Y,_ },
            new[]{ _, Y,Y,Y,Y,Y,Y,_,_,Y,Y,Y,Y,Y,Y,_ },
            new[]{ _, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,_ },
            new[]{ _, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,_ },
            new[]{ _, Y,Y,Y,Y,E,Y,Y,Y,Y,E,Y,Y,Y,Y,_ },
            new[]{ _, Y,Y,Y,Y,Y,E,E,E,E,Y,Y,Y,Y,Y,_ },
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },
            new[]{ _,_,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_,_ },
            new[]{ _,_,_,_, Y,Y,Y,Y,Y,Y,Y,Y, _,_,_,_ },
            new[]{ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ },
            new[]{ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ },
        };

        var pixels = new Color[16 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            pixels[y * 16 + x] = rows[y][x];

        var tex = new Texture2D(gd, 16, 16);
        tex.SetData(pixels);
        return tex;
    }

    private void GoToScreenA() => MoveToScreen<DisplaySettingsDemoScreenA>();
    private void GoToScreenC() => MoveToScreen<DisplaySettingsDemoScreenC>();
}
