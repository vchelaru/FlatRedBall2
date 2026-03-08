using FlatRedBall2;
using TopDownMenuSampleWithCodeGen.Screens;

namespace TopDownMenuSampleWithCodeGen;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private Microsoft.Xna.Framework.GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new Microsoft.Xna.Framework.GraphicsDeviceManager(this);
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

    protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
