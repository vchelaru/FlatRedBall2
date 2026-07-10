using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ShapeFillColorRepro;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes needs SM 4.0+ — default Reach profile crashes at startup.
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        Content.RootDirectory = "Content";
        FlatRedBall2.FlatRedBallService.Default.PrepareWindow<ReproScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBall2.FlatRedBallService.Default.Initialize(this);
        FlatRedBall2.FlatRedBallService.Default.Start<ReproScreen>();
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
