using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TopDownMenuSample.Screens;

public class PauseMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 20);

        // Load the Gum screen for the semi-transparent overlay and title.
        var gumScreen = ObjectFinder.Self.GumProjectSave.Screens
            .Find(s => s.Name == "PauseMenuScreen");
        Add(gumScreen!.ToGraphicalUiElement());

        var menu = new StackPanel();
        menu.Spacing = 12;
        menu.Anchor(Anchor.Center);

        var resumeButton = new Button();
        resumeButton.Text = "Resume";
        resumeButton.Click += (_, _) => MoveToScreen<GameScreen>();
        menu.AddChild(resumeButton);

        var mainMenuButton = new Button();
        mainMenuButton.Text = "Exit to Main Menu";
        mainMenuButton.Click += (_, _) => MoveToScreen<MainMenuScreen>();
        menu.AddChild(mainMenuButton);

        Add(menu);
    }

    public override void CustomActivity(FrameTime time) { }
}
