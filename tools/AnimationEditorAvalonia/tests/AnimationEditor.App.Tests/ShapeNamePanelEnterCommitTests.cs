using AnimationEditor.Core;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression test for #1118: PropRectName/PropCircleName only wired LostFocus, so Enter did
/// nothing while every other editable text field in the property panel (PropAlpha, PropTextureName,
/// PropRed/Green/Blue) already commits on Enter too (see InspectorChannelCommitTests). Enter must
/// commit the name without requiring focus to move elsewhere.
/// </summary>
public class ShapeNamePanelEnterCommitTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void RectNameField_EditThenEnter_CommitsWithoutFocusLoss()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
            var rect = new AARectSave { Name = "Box0" };
            frame.ShapesSave!.Shapes.Add(rect);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedRectangle = rect;
            Dispatcher.UIThread.RunJobs();

            var nameInput = window.FindControl<TextBox>("PropRectName")!;
            nameInput.Text = "Hitbox";
            nameInput.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("Hitbox", rect.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CircleNameField_EditThenEnter_CommitsWithoutFocusLoss()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
            var circle = new CircleSave { Name = "Circ0" };
            frame.ShapesSave!.Shapes.Add(circle);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedCircle = circle;
            Dispatcher.UIThread.RunJobs();

            var nameInput = window.FindControl<TextBox>("PropCircleName")!;
            nameInput.Text = "Hurtbox";
            nameInput.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal("Hurtbox", circle.Name);
        }
        finally { window.Close(); }
    }
}
