using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AndroidSpike;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            // KNI's Android backend rejects FL10_0 ("Adapter '' does not support the FL10_0 profile")
            // even on an OpenGL ES 3.2 device, so this spike uses HiDef.
            GraphicsProfile = GraphicsProfile.HiDef,
            IsFullScreen = true,
        };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize<GameScreen>(this);
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
