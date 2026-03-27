using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Screens;

namespace SampleProject1;

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

        FlatRedBall2.FlatRedBallService.Default.DisplaySettings.Zoom = 1;
        FlatRedBall2.FlatRedBallService.Default.DisplaySettings.ResolutionWidth = 1280;
        FlatRedBall2.FlatRedBallService.Default.DisplaySettings.ResolutionHeight = 720;

        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<CoinCollectorScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        FlatRedBall2.FlatRedBallService.Default.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBall2.FlatRedBallService.Default.Draw();

        base.Draw(gameTime);
    }
}
