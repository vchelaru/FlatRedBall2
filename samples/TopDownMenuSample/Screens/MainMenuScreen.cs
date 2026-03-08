using FlatRedBall2;
using Gum.Forms;
using Gum.Forms.Controls;
using Gum.Managers;
using Microsoft.Xna.Framework;
using MonoGameGum;

namespace TopDownMenuSample.Screens;

public class MainMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 15, 40);

        var gumScreen = ObjectFinder.Self.GumProjectSave.Screens
            .Find(s => s.Name == "MainMenuScreen");
        var root = gumScreen!.ToGraphicalUiElement();
        Add(root);

        var startButton = root.GetFrameworkElementByName<Button>("StartGameButton");
        startButton.Click += (_, _) => MoveToScreen<GameScreen>();

        var exitButton = root.GetFrameworkElementByName<Button>("ExitButton");
        exitButton.Click += (_, _) => Engine.Game.Exit();
    }

    public override void CustomActivity(FrameTime time) { }
}
