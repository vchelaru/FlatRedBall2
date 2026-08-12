using FlatRedBall2;
using FlatRedBall2.Glue;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GlueLoaderScratch;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    // Relative, and it has to stay relative: FlatRedBallService derives GlueContentSource's ContentRoot
    // from this path's directory, and that resolves through TitleContainer, which throws on an absolute
    // path. An absolute .gluj therefore loads fine and then fails every texture/CSV it references.
    // The engine reads it from OutputContentRoot, so the working directory does not matter.
    private const string GlueProjectFile = "Content/FrbEditor/GlueLoaderScratch.gluj";

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
#if KNI
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
#else
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize(this, GlueProjectFile);

        // Inert without --frb-auto. Present so a discrepancy in this test bed can be checked by
        // stepping frames and capturing a screenshot rather than by eye.
        FlatRedBallService.Default.EnableAutomationMode();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }
}
