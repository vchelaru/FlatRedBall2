using Microsoft.Xna.Framework;
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
        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        // Portrait 240x320 design res, scaled 3x to a 720x960 window.
        // IncreaseVisibleArea with Zoom=3 gives exactly 3 screen pixels per world unit
        // (crisp pixel scaling) while keeping the visible world at 240x320.
        var ds = FlatRedBallService.Default.DisplaySettings;
        ds.ResolutionWidth = 240;
        ds.ResolutionHeight = 320;
        ds.PreferredWindowWidth = 720;
        ds.PreferredWindowHeight = 960;
        ds.Zoom = 3f;
        ds.ResizeMode = ResizeMode.IncreaseVisibleArea;

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
