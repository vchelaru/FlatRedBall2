using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FlatRedBall2;
using PlatformKing.Screens;

namespace PlatformKing;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes needs SM 4.0+; KNI's FL10_0 profile provides it. The Reach default crashes at startup.
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        // 426x240 design resolution — pixel-art scale comes from window-vs-resolution.
        // 1280x720 desktop window = ~3 px/world unit. KNI ignores window size (canvas DOM drives it).
        var ds = FlatRedBallService.Default.DisplaySettings;
        ds.ResolutionWidth = 426;
        ds.ResolutionHeight = 240;
        ds.PreferredWindowWidth = 1280;
        ds.PreferredWindowHeight = 720;
        ds.AllowUserResizing = true;
        FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize(this);
        FlatRedBallService.Default.Start<GameScreen>();
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
