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
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
    }

    protected override void Initialize()
    {
        base.Initialize();

        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<GameScreen>();
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
