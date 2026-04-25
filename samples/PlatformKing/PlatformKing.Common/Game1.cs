using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FlatRedBall2;
using PlatformKing.Screens;

namespace PlatformKing;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes needs SM 4.0+. MonoGame tops out at HiDef; KNI needs FL10_0 for equivalent.
#if KNI
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
#else
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        // Required on KNI BlazorGL so the canvas back buffer tracks the canvas DOM size.
        // The engine skips ApplyWindowSettings only when AllowUserResizing=true AND
        // no PreferredDisplaySettings is set. Harmless on DesktopGL.
        Window.AllowUserResizing = true;
        FlatRedBallService.Default.DisplaySettings.Zoom = 3f;
        FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize(this);
        FlatRedBallService.Default.Start<GameScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
