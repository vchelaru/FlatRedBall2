using FlatRedBall2;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGameGum;

namespace TopDownMenuSample.Screens;

public class GameScreen : Screen
{
    private GraphicalUiElement _pauseOverlay = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 20);

        var gumScreen = ObjectFinder.Self.GumProjectSave.Screens
            .Find(s => s.Name == "PauseMenuScreen");
        _pauseOverlay = gumScreen!.ToGraphicalUiElement();
        _pauseOverlay.Visible = false;
        Add(_pauseOverlay);

        var resumeButton = _pauseOverlay.GetFrameworkElementByName<Button>("ResumeButton");
        resumeButton.Click += (_, _) => Resume();

        var exitToMenuButton = _pauseOverlay.GetFrameworkElementByName<Button>("ExitToMenuButton");
        exitToMenuButton.Click += (_, _) => MoveToScreen<MainMenuScreen>();
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }
    }

    private void Pause()
    {
        PauseThisScreen();
        _pauseOverlay.Visible = true;
    }

    private void Resume()
    {
        ResumeThisScreen();
        _pauseOverlay.Visible = false;
    }
}
