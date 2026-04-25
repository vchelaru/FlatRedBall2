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

        // Fixed 720x960 portrait window/canvas on both backends. The BlazorGL canvas DOM is
        // CSS-constrained to 720x960 in Pages/Index.razor, so the back buffer and canvas display
        // size match — no stretch. (Contrast: a stretch-to-viewport canvas would set
        // Window.AllowUserResizing=true and leave PreferredWindowWidth/Height unset.)
        var ds = FlatRedBallService.Default.DisplaySettings;
        ds.ResolutionWidth = 240;
        ds.ResolutionHeight = 320;
        ds.Zoom = 3f;
        ds.ResizeMode = ResizeMode.IncreaseVisibleArea;
        ds.PreferredWindowWidth = 720;
        ds.PreferredWindowHeight = 960;
        ds.AllowUserResizing = false;

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
