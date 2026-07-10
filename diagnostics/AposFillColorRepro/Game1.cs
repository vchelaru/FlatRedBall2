using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AposFillColorRepro;

// Pure MonoGame + Apos.Shapes regression check for FlatRedBall2 issue #663 — NO FlatRedBall2 involved.
//
// Apos packs each pair of color bytes into one float (a Szudzik pairing) and decodes it in the shader
// with floor(sqrt(n)). On macOS's GL driver, sqrt of a perfect square lands just below the integer
// (sqrt(65025) = 254.9999...), so floor() drops a step and a color byte that should be 0 decodes as
// ~255. Blue is paired with alpha, so an opaque black fill rendered blue. The fix is in Apos's
// Unpair(), shipped via FlatRedBall2's precompiled apos-shapes.xnb.
//
// This draws a black rectangle on light gray. Correct = BLACK. Regression (#663 back) = BLUE.
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private ShapeBatch _shapes = null!;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.GraphicsProfile = GraphicsProfile.HiDef; // Apos.Shapes needs SM 4.0+
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _shapes = new ShapeBatch(GraphicsDevice, Content);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        Window.Title = "Apos #663 regression check — rectangle should be BLACK (blue = regressed)";
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.LightGray);
        _shapes.Begin();
        _shapes.FillRectangle(new Vector2(250, 140), new Vector2(300, 200), Color.Black, aaSize: 0f);
        _shapes.End();
        base.Draw(gameTime);
    }
}
