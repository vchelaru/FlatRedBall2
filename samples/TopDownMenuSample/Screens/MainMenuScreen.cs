using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Managers;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TopDownMenuSample.Screens;

public class MainMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 15, 40);

        // Load the Gum screen for background and title from the project file.
        var gumScreen = ObjectFinder.Self.GumProjectSave.Screens
            .Find(s => s.Name == "MainMenuScreen");
        Add(gumScreen!.ToGraphicalUiElement());

        var menu = new StackPanel();
        menu.Spacing = 12;
        menu.Anchor(Anchor.Center);
        menu.Y = 40;

        var startButton = new Button();
        startButton.Text = "Start Game";
        startButton.Click += (_, _) => MoveToScreen<GameScreen>();
        menu.AddChild(startButton);

        var exitButton = new Button();
        exitButton.Text = "Exit";
        exitButton.Click += (_, _) => Engine.Game.Exit();
        menu.AddChild(exitButton);

        Add(menu);
    }

    public override void CustomActivity(FrameTime time) { }
}
