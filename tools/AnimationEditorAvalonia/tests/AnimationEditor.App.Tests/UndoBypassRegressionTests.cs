using AnimationEditor.Core;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression coverage for an App-layer mutation path that used to bypass the undo
/// stack entirely: the flip toggle buttons must route through <c>IUndoManager.Execute</c>.
///
/// <para>Multi-select delete is <em>not</em> covered here — that logic lives on
/// <c>AppCommands</c> and is exercised by pure <c>[Fact]</c> tests in
/// <c>AnimationEditor.Core.Tests.AppCommandsDeleteAsyncTests</c>. Likewise the paste
/// path's mutating core is covered by
/// <c>AnimationEditor.Core.Tests.AppCommandsPasteTests</c>. Only the genuinely
/// UI-bound wiring stays in this headless-Avalonia file.</para>
/// </summary>
public class UndoBypassRegressionTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;

        // Note: the MainWindow constructor re-wires AppCommands.ConfirmAsync /
        // PromptStringAsync / FileDialogService to its own dialogs, so any test stub
        // for those must be assigned *after* CreateMainWindow(), not before.
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    /// <summary>Adds a chain with <paramref name="frameCount"/> shaped frames to the project.</summary>
    private static AnimationChainSave MakeChain(TestServices ctx, string name, int frameCount)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"f{i}.png", ShapesSave = new ShapesSave() });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        return chain;
    }

    // ── Flip toggle buttons ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void FlipHorizontalToggle_Checked_IsUndoable()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx, "Walk", 1);
            var frame = chain.Frames[0];
            ctx.SelectedState.SelectedFrame = frame;

            var flipH = window.FindControl<ToggleButton>("PropFlipH")!;
            flipH.IsChecked = true; // user clicks the flip button

            Assert.True(frame.FlipHorizontal);
            Assert.True(ctx.UndoManager.CanUndo);

            ctx.UndoManager.Undo();
            Assert.False(frame.FlipHorizontal);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Undo_AfterFlip_ResyncsFlipToggleButton()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = MakeChain(ctx, "Walk", 1);
            var frame = chain.Frames[0];
            ctx.SelectedState.SelectedFrame = frame;

            var flipH = window.FindControl<ToggleButton>("PropFlipH")!;
            flipH.IsChecked = true;

            ctx.UndoManager.Undo();
            // The toggle re-syncs via HandleAnimationChainsChanged →
            // Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel); pump the
            // dispatcher queue so that runs before asserting on the button.
            Dispatcher.UIThread.RunJobs();

            // The model is unflipped again, so the button must not stay lit.
            Assert.False(frame.FlipHorizontal);
            Assert.False(flipH.IsChecked == true);
        }
        finally { window.Close(); }
    }
}
