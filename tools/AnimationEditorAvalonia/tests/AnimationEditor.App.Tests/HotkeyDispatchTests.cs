using AnimationEditor.App.Controls;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #632: hotkeys are now dispatched through a shared registry (BuildHotkeyDefinitions)
/// instead of a hand-written if/else chain. These cover the branches that had no prior
/// KeyPress-simulation coverage — Undo/Redo (both Redo gestures), F3, Space and Alt+Up reorder —
/// to prove the refactor preserved their behavior. Copy/Cut/Paste/Duplicate/Delete's text-input
/// focus gating already has coverage in CopyPasteFocusGateTests/DuplicateShortcutTests.
/// </summary>
public class HotkeyDispatchTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static TreeNodeVm SeedAndSelect(MainWindow window, object data, string header)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        var vm = new TreeNodeVm { Header = header, Data = data };
        roots.Add(vm);
        tree.SelectedItems!.Add(vm);
        tree.Focus();
        Dispatcher.UIThread.RunJobs();
        return vm;
    }

    [AvaloniaFact]
    public void CtrlZ_AfterDeletingChain_RestoresChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Walk");

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);

            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CtrlY_AfterUndoingDelete_ReDeletesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Walk");

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains); // restored

            window.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    /// <summary>Redo's second gesture (Ctrl+Shift+Z) must behave identically to Ctrl+Y.</summary>
    [AvaloniaFact]
    public void CtrlShiftZ_AfterUndoingDelete_ReDeletesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Walk");

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains); // restored

            window.KeyPress(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void F3_TogglesRenderDiagnosticsOnBothPanels()
    {
        var (window, _) = CreateWindow();
        try
        {
            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            Assert.False(wireframe.DiagnosticsEnabled);
            Assert.False(preview.DiagnosticsEnabled);

            window.KeyPress(Key.F3, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(wireframe.DiagnosticsEnabled);
            Assert.True(preview.DiagnosticsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Space_TreeFocused_TogglesPreviewPlayback()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Walk");
            ctx.SelectedState.SelectedChain = chain;

            // Selecting a chain auto-plays its preview, so Space (a toggle) pauses it first.
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            Assert.True(preview.IsPlaying);

            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(preview.IsPlaying);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AltUp_SecondChainSelected_MovesItAboveFirst()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var first = new AnimationChainSave { Name = "First" };
            var second = new AnimationChainSave { Name = "Second" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(first);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(second);
            SeedAndSelect(window, second, "Second");

            window.KeyPress(Key.Up, RawInputModifiers.Alt, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(second, ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0]);
            Assert.Same(first, ctx.ProjectManager.AnimationChainListSave!.AnimationChains[1]);
        }
        finally { window.Close(); }
    }

    /// <summary>Guards against two hotkeys silently shadowing one another as the registry grows.</summary>
    [AvaloniaFact]
    public void Hotkeys_ProductionRegistry_HasNoDuplicateGestures()
    {
        var (window, _) = CreateWindow();
        try
        {
            var duplicates = HotkeyRegistry.FindDuplicateGestures(window.Hotkeys.ToList());

            Assert.Empty(duplicates);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Menu InputGesture text is generated from the registry (ApplyHotkeyMenuGestureText), so it
    /// can never drift from what the KeyDown handler dispatches — the bug this issue fixes.
    /// </summary>
    [AvaloniaFact]
    public void MenuUndo_InputGesture_MatchesCtrlZDispatchedByKeyDown()
    {
        var (window, _) = CreateWindow();
        try
        {
            var menuUndo = window.FindControl<MenuItem>("MenuUndo")!;

            Assert.Equal(new KeyGesture(Key.Z, KeyModifiers.Control), menuUndo.InputGesture);
        }
        finally { window.Close(); }
    }
}
