using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Screens;

/// <summary>
/// Display settings demo — Screen C.
/// Zoom=3, IncreaseVisibleArea. When this is the starting screen, the window locks to 1440×960
/// with resizing disabled so each world unit maps to exactly 3 screen pixels (pixel-art upscale).
/// When navigated to from Screen B, only camera settings apply — the window stays at its current size.
/// </summary>
public class DisplaySettingsDemoScreenC : Screen
{
    public override DisplaySettings PreferredDisplaySettings => new DisplaySettings
    {
        Zoom = 3f,
        ResizeMode = ResizeMode.IncreaseVisibleArea,
        // Window properties: only applied when this is the starting screen, not during navigation.
        PreferredWindowWidth = 1440,
        PreferredWindowHeight = 960,
        AllowUserResizing = false,
    };

    private Label _infoLabel = null!;
    private Texture2D _demoTexture = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(35, 15, 40);

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
        titleLabel.Text = "Screen C — Zoom 3×, 480×320 inner res, fixed 1440×960 window";
        stack.AddChild(titleLabel);

        var hintLabel = new Label();
        hintLabel.Text = "Window is fixed at 1440×960 (3× the 480×320 inner resolution); resizing disabled  |  F11: toggle fullscreen";
        stack.AddChild(hintLabel);

        _infoLabel = new Label();
        stack.AddChild(_infoLabel);

        hud.AddChild(stack);

        var btn = new Button();
        btn.Text = "← Screen B  (Zoom 2×, no AR lock, IncreaseVisibleArea)";
        btn.Anchor(Anchor.BottomRight);
        btn.X = -16;
        btn.Y = -16;
        btn.Click += (_, _) => GoToScreenB();
        hud.AddChild(btn);

        Add(hud);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.F11))
            ToggleFullscreen();

        float visibleW = Camera.TargetWidth / Camera.Zoom;
        float visibleH = Camera.TargetHeight / Camera.Zoom;
        var mode = Engine.DisplaySettings.WindowMode == WindowMode.FullscreenBorderless ? "Fullscreen" : "Windowed";
        _infoLabel.Text = $"Camera: TargetWidth={Camera.TargetWidth}  TargetHeight={Camera.TargetHeight}  Zoom={Camera.Zoom:F1}  " +
                          $"Visible world: {visibleW:F0}×{visibleH:F0}  [{mode}]";

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

        Engine.Overlay.Rectangle(0, 0, hw * 2f, hh * 2f, Color.MediumPurple);
        Engine.Overlay.Line(-hw, 0, hw, 0, new Color(80, 40, 100));
        Engine.Overlay.Line(0, -hh, 0, hh, new Color(80, 40, 100));
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

    private void GoToScreenB() => MoveToScreen<DisplaySettingsDemoScreenB>();
}
