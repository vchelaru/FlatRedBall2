using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PongGravity.Screens;

namespace PongGravity;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        var ds = FlatRedBall2.FlatRedBallService.Default.DisplaySettings;
        ds.PreferredWindowWidth  = 2560;
        ds.PreferredWindowHeight = 1440;
        FlatRedBall2.FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBall2.FlatRedBallService.Default.Initialize(this, new FlatRedBall2.EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx"
        });
        FlatRedBall2.FlatRedBallService.Default.Start<GameScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        FlatRedBall2.FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBall2.FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
