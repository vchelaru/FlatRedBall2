using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TopDownMenuSample.Screens;

namespace TopDownMenuSample;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        FlatRedBallService.Default.Initialize(this, new EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx"
        });
        FlatRedBallService.Default.Start<MainMenuScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
