using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FlatRedBall2;
using FlatRedBall2.Rendering;
using ShmupSpace.Screens;

namespace ShmupSpace;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes (used transitively by FlatRedBall2 shape rendering) needs SM 4.0+.
        // MonoGame tops out at HiDef; KNI needs FL10_0 for the equivalent profile.
#if KNI
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
#else
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        // Required on KNI BlazorGL so the canvas back buffer tracks the canvas DOM size.
        // The engine skips ApplyWindowSettings only when AllowUserResizing=true AND no per-screen
        // PreferredDisplaySettings is set. Harmless on DesktopGL.
        Window.AllowUserResizing = true;

        // Portrait 240x320 design res. Camera Zoom=3 with IncreaseVisibleArea gives 3 screen
        // pixels per world unit (crisp pixel scaling). On Desktop we also pick a 720x960 window
        // size; on KNI we leave PreferredWindowWidth/Height unset so the browser canvas drives
        // the back-buffer size — otherwise PrepareWindow clamps the back buffer to 720x960 while
        // the canvas stays at 100vw/100vh, breaking Camera.ScreenToWorld.
        var ds = FlatRedBallService.Default.DisplaySettings;
        ds.ResolutionWidth = 240;
        ds.ResolutionHeight = 320;
        ds.Zoom = 3f;
        ds.ResizeMode = ResizeMode.IncreaseVisibleArea;
#if !KNI
        ds.PreferredWindowWidth = 720;
        ds.PreferredWindowHeight = 960;
#endif

        FlatRedBallService.Default.PrepareWindow<TitleScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize(this);
        FlatRedBallService.Default.Start<TitleScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
