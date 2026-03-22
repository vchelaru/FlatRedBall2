using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ZeldaRoomsSample.Screens;

namespace ZeldaRoomsSample;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        FlatRedBall2.FlatRedBallService.Default.DisplaySettings.PreferredWindowWidth = 1280;
        FlatRedBall2.FlatRedBallService.Default.DisplaySettings.PreferredWindowHeight = 720;
        FlatRedBall2.FlatRedBallService.Default.PrepareWindow<GameplayScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<GameplayScreen>();
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
