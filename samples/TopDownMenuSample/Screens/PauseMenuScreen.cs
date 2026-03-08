using FlatRedBall2;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Managers;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TopDownMenuSample.Screens;

public class PauseMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 20);

        var gumScreen = ObjectFinder.Self.GumProjectSave.Screens
            .Find(s => s.Name == "PauseMenuScreen");
        var root = gumScreen!.ToGraphicalUiElement();
        Add(root);

        var resumeButton = root.GetFrameworkElementByName<Button>("ResumeButton");
        resumeButton.Click += (_, _) => MoveToScreen<GameScreen>();

        var exitToMenuButton = root.GetFrameworkElementByName<Button>("ExitToMenuButton");
        exitToMenuButton.Click += (_, _) => MoveToScreen<MainMenuScreen>();
    }

    public override void CustomActivity(FrameTime time) { }
}
