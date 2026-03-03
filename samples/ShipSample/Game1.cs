using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ShipSample;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<GameScreen>();
    }

    protected override void Update(GameTime gt)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        FlatRedBall2.FlatRedBallService.Default.Update(gt);
        base.Update(gt);
    }

    protected override void Draw(GameTime gt)
    {
        FlatRedBall2.FlatRedBallService.Default.Draw();
        base.Draw(gt);
    }
}
