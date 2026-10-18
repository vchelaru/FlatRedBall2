using Beefball.Screens;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Beefball;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly ScreenshotDriver? _screenshots;
    private readonly bool _selfTest;
    private bool _selfTestDone;

    public Game1(string? screenshotDirectory = null, bool selfTest = false)
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes needs shader model 4.0+; the default Reach profile crashes at startup.
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        Content.RootDirectory = "Content";
        IsMouseVisible = false;

        var display = FlatRedBallService.Default.DisplaySettings;
        display.ResolutionWidth = Arena.ResolutionWidth;
        display.ResolutionHeight = Arena.ResolutionHeight;
        display.PreferredWindowWidth = Arena.ResolutionWidth;
        display.PreferredWindowHeight = Arena.ResolutionHeight;

        _selfTest = selfTest;
        if (screenshotDirectory != null)
            _screenshots = new ScreenshotDriver(screenshotDirectory);
    }

    protected override void Initialize()
    {
        base.Initialize();
        FlatRedBallService.Default.Initialize<TitleScreen>(this);
    }

    protected override void Update(GameTime gameTime)
    {
        if (_selfTest && !_selfTestDone && gameTime.TotalGameTime > TimeSpan.FromMilliseconds(100))
        {
            _selfTestDone = true;
            Environment.ExitCode = new SelfTest().Run(FlatRedBallService.Default);
            Exit();
            return;
        }

        FlatRedBallService.Default.Update(gameTime);
        _screenshots?.Update(FlatRedBallService.Default, this);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        _screenshots?.AfterDraw(GraphicsDevice);
        base.Draw(gameTime);
    }
}
