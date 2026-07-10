using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AposFillColorRepro;

// Pure MonoGame + Apos.Shapes repro for issue #663 — NO FlatRedBall2 involved.
// On macOS DesktopGL a black FillRectangle renders with the blue channel forced to 1.0 (comes out
// blue). It's correct on Windows. This isolates the bug to Apos.Shapes + the macOS GL driver.
//
// Press number keys to change what is drawn BEFORE the black rectangle each frame, to find what
// "heals" it (real projects self-heal once other content draws):
//   0 = nothing               (baseline — expect BLUE on macOS)
//   1 = a SpriteBatch sprite  (plain textured draw on unit 0, separate pipeline)
//   2 = an Apos textured Draw  (Apos's textured shader branch, unit 0)
//   3 = another Apos fill      (FillCircle — same shader branch as the bug)
// Whichever mode turns the rectangle BLACK reveals the mechanism.
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private ShapeBatch _shapes = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _texture = null!;
    private int _healMode;

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
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _texture = new Texture2D(GraphicsDevice, 2, 2);
        _texture.SetData(new[] { Color.White, Color.White, Color.White, Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape)) Exit();
        if (kb.IsKeyDown(Keys.D0)) _healMode = 0;
        if (kb.IsKeyDown(Keys.D1)) _healMode = 1;
        if (kb.IsKeyDown(Keys.D2)) _healMode = 2;
        if (kb.IsKeyDown(Keys.D3)) _healMode = 3;
        Window.Title = $"Apos #663 repro — heal mode {_healMode} (keys 0-3). Rectangle should be BLACK.";
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.LightGray);

        // Optional "healer" drawn before the black fill.
        switch (_healMode)
        {
            case 1: // plain SpriteBatch sprite
                _spriteBatch.Begin();
                _spriteBatch.Draw(_texture, new Vector2(10, 10), Color.White);
                _spriteBatch.End();
                break;
            case 2: // Apos textured draw — exercises the shader's textured branch on unit 0
                _shapes.Begin();
                _shapes.Draw(_texture, new Vector2(10, 10));
                _shapes.End();
                break;
            case 3: // another Apos fill — same shader branch as the bug
                _shapes.Begin();
                _shapes.FillCircle(new Vector2(40, 40), 10f, Color.White, aaSize: 0f);
                _shapes.End();
                break;
        }

        // The bug: a black filled rectangle. On macOS it renders blue when nothing above healed it.
        _shapes.Begin();
        _shapes.FillRectangle(new Vector2(250, 140), new Vector2(300, 200), Color.Black, aaSize: 0f);
        _shapes.End();

        base.Draw(gameTime);
    }
}
