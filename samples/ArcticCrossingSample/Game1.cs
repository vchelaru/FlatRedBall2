using ArcticCrossingSample.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ArcticCrossingSample;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        var ds = FlatRedBall2.FlatRedBallService.Default.DisplaySettings;
        ds.ResolutionWidth = 1280;
        ds.ResolutionHeight = 720;
        ds.PreferredWindowWidth = 1280;
        ds.PreferredWindowHeight = 720;
        ds.AllowUserResizing = true;
        ds.FixedAspectRatio = 16f / 9f;
        ds.LetterboxColor = Color.Black;
        FlatRedBall2.FlatRedBallService.Default.PrepareWindow<TitleScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<TitleScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        FlatRedBall2.FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBall2.FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
