using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Covers <c>MainWindow.HandlePngDropAsync</c> — the single apply path shared by the ANIMATIONS
/// tree's PNG drop and the wireframe canvas's PNG drop (issue #560). The pure decision/apply
/// logic it delegates to (<c>TextureDropProcessor.ComputePngDrop</c>, <c>TextureDropApplier</c>,
/// <c>TextureCopyDecider</c>) is covered directly in <c>AnimationEditor.Core.Tests</c>; what's
/// left here is the orchestration around it (skip-the-copy-prompt-when-in-folder branch, refresh
/// side effects). Avalonia's <see cref="DragEventArgs"/> can't be constructed from test code (no
/// existing test in this suite does so — see the class comment on
/// <c>FrameDragReorderHeadlessTests</c> for the same limitation with drag gestures), so these
/// invoke the method directly via reflection with plain domain objects, bypassing the untestable
/// Drop-event plumbing on either surface.
/// </summary>
public class PngDropApplyTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = TestPaths.Abs("Project", "Animations", "Hero.achx");
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static Task<bool> InvokeHandlePngDrop(
        MainWindow window, AnimationChainSave? chain, AnimationFrameSave? frame, string droppedFilePath, bool ctrlHeld)
    {
        var method = typeof(MainWindow).GetMethod("HandlePngDropAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task<bool>)method.Invoke(window, new object?[] { chain, frame, droppedFilePath, ctrlHeld })!;
    }

    [AvaloniaFact]
    public async Task HandlePngDropAsync_NonPngFile_ReturnsFalseAndLeavesFrameUntouched()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "old.png" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            string droppedPath = TestPaths.Abs("Project", "Animations", "notes.txt");

            bool applied = await InvokeHandlePngDrop(window, chain, frame, droppedPath, ctrlHeld: false);

            Assert.False(applied);
            Assert.Equal("old.png", frame.TextureName);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task HandlePngDropAsync_TextureInsideAchxFolder_UpdatesFrameTextureNameWithoutPrompt()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "old.png" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;

            // Inside the achx folder (TestPaths.Abs("Project", "Animations")) — no copy prompt
            // should fire, so this must complete without ShowDialog ever being awaited.
            string droppedPath = TestPaths.Abs("Project", "Animations", "new.png");

            bool applied = await InvokeHandlePngDrop(window, chain, frame, droppedPath, ctrlHeld: false);

            Assert.True(applied);
            Assert.Equal("new.png", frame.TextureName);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void WireframeControl_Constructed_AllowsDrop()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreateWireframeControl();

        Assert.True(DragDrop.GetAllowDrop(ctrl));
    }
}
