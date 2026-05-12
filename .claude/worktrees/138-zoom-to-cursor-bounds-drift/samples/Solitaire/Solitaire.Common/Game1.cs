using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Solitaire.Screens;

namespace Solitaire;

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
        IsMouseVisible = true;

        var ds = FlatRedBallService.Default.DisplaySettings;
        const int height = 640;
        ds.ResolutionWidth = 800;
        ds.ResolutionHeight = height;
        ds.PreferredWindowWidth = 800;
        ds.PreferredWindowHeight = height;
        ds.AllowUserResizing = true;

        FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        // Path is relative to the MonoGame Content root; do NOT prepend "Content/".
        FlatRedBallService.Default.Initialize(this, new EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx"
        });
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
