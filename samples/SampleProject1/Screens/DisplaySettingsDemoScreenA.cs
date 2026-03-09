using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Screens;

/// <summary>
/// Display settings demo — Screen A.
/// Zoom=1, StretchVisibleArea, FixedAspectRatio=16:9.
/// Resize the window: black bars maintain 16:9; the world stretches to fill the letterboxed area.
/// </summary>
public class DisplaySettingsDemoScreenA : Screen
{
    public override DisplaySettings PreferredDisplaySettings => new DisplaySettings
    {
        Zoom = 1f,
        ResizeMode = ResizeMode.StretchVisibleArea,
        FixedAspectRatio = 16f / 9f,
        AllowUserResizing = true,
    };

    private Label _infoLabel = null!;
    private Texture2D _demoTexture = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 20, 45);

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
        titleLabel.Text = "Screen A — Zoom 1×, StretchVisibleArea, 16:9 locked";
        stack.AddChild(titleLabel);

        var hintLabel = new Label();
        hintLabel.Text = "Resize window → bars keep 16:9; world stretches to fill it  |  F11: toggle fullscreen";
        stack.AddChild(hintLabel);

        _infoLabel = new Label();
        stack.AddChild(_infoLabel);

        hud.AddChild(stack);

        var btn = new Button();
        btn.Text = "→ Screen B  (Zoom 2×, no AR lock, IncreaseVisibleArea)";
        btn.Anchor(Anchor.BottomRight);
        btn.X = -16;
        btn.Y = -16;
        btn.Click += (_, _) => GoToScreenB();
        hud.AddChild(btn);

        Add(hud);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.F11))
            ToggleFullscreen();

        var mode = Engine.DisplaySettings.WindowMode == WindowMode.FullscreenBorderless ? "Fullscreen" : "Windowed";
        _infoLabel.Text = $"Camera: TargetWidth={Camera.TargetWidth}  TargetHeight={Camera.TargetHeight}  Zoom={Camera.Zoom:F1}  [{mode}]";

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

        Engine.Overlay.Rectangle(0, 0, hw * 2f, hh * 2f, Color.CornflowerBlue);
        Engine.Overlay.Line(-hw, 0, hw, 0, new Color(80, 80, 120));
        Engine.Overlay.Line(0, -hh, 0, hh, new Color(80, 80, 120));
    }

    public override void CustomDestroy()
    {
        _demoTexture?.Dispose();
    }

    private static Texture2D CreateSmileyTexture(GraphicsDevice gd)
    {
        // 16×16 pixel-art smiley. PointClamp (engine default) makes each
        // pixel crisp at any zoom level — the zoom difference is clearly visible.
        var Y = new Color(255, 200, 0);   // amber face
        var E = new Color(50, 30, 10);    // dark brown (eyes + mouth)
        var _ = Color.Transparent;

        var rows = new[]
        {
            new[]{ _,_,_,_, Y,Y,Y,Y,Y,Y,Y,Y, _,_,_,_ },  // 0
            new[]{ _,_,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_,_ },  // 1
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },  // 2
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },  // 3
            new[]{ _, Y,Y,Y,Y,Y,Y,_,_,Y,Y,Y,Y,Y,Y,_ },   // 4  eyes
            new[]{ _, Y,Y,Y,Y,Y,Y,_,_,Y,Y,Y,Y,Y,Y,_ },   // 5  eyes
            new[]{ _, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,_ },   // 6
            new[]{ _, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,_ },   // 7
            new[]{ _, Y,Y,Y,Y,E,Y,Y,Y,Y,E,Y,Y,Y,Y,_ },   // 8  mouth corners
            new[]{ _, Y,Y,Y,Y,Y,E,E,E,E,Y,Y,Y,Y,Y,_ },   // 9  smile bottom
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },  // 10
            new[]{ _,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_ },  // 11
            new[]{ _,_,_, Y,Y,Y,Y,Y,Y,Y,Y,Y,Y, _,_,_ },  // 12
            new[]{ _,_,_,_, Y,Y,Y,Y,Y,Y,Y,Y, _,_,_,_ },  // 13
            new[]{ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ },   // 14
            new[]{ _,_,_,_,_,_,_,_,_,_,_,_,_,_,_,_ },   // 15
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
