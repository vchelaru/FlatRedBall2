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
        // Apos.Shapes needs SM 4.0+; KNI's FL10_0 profile provides it. The Reach default crashes at startup.
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
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
        // Path is relative to the Content root; do NOT prepend "Content/".
        // Gum 2026.6 selects loose vs. bundle purely from the extension (no sibling probing),
        // so this must match the deployed artifact. The build defines GUM_BUNDLE when
        // UseGumPackage=true (the default) — see Solitaire.GumPackage.targets.
        FlatRedBallService.Default.Initialize(this, new EngineInitSettings
        {
#if GUM_BUNDLE
            GumProjectFile = "GumProject/GumProject.gumpkg",
#else
            GumProjectFile = "GumProject/GumProject.gumj",
#endif
            // Solitaire's cards zoom with Camera.Zoom, so crisp re-rasterized text matters here —
            // most FRB2 projects default this off (see EngineInitSettings.UseFontOversampling).
            UseFontOversampling = true
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
