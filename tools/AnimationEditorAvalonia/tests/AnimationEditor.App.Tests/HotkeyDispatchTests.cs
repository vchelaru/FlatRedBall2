using AnimationEditor.App.Controls;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.ObjectModel;
using System.IO;
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

    // ── Issue #638: decorative File/View shortcuts wired to real actions ───────

    [AvaloniaFact]
    public void CtrlN_ClearsTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var vm = SeedAndSelect(window, chain, "Walk");
            var roots = (ObservableCollection<TreeNodeVm>)window.FindControl<TreeView>("AnimTree")!.ItemsSource!;
            Assert.Contains(vm, roots);

            window.KeyPress(Key.N, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain(vm, roots);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CtrlS_WithFileName_WritesFileToDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "out.achx");
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
                new AnimationChainSave { Name = "Idle" });
            ctx.ProjectManager.FileName = path;

            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(File.Exists(path));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The headless platform's StorageProvider returns no files for a file-open request, so this
    /// only proves Ctrl+L dispatches to the same load path as clicking MenuLoad without throwing —
    /// it does not exercise picking and loading a real file (no test in this suite does; loading a
    /// specific file is covered via LoadAnimationFileAsync directly, see MainWindowMenuFlowTests).
    /// </summary>
    [AvaloniaFact]
    public void CtrlL_DoesNotThrow()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            window.KeyPress(Key.L, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }

    // Clicks the centre of `control` via a real pointer press, so focus-on-click behavior
    // (TextureViewport/PreviewControl.OnPointerPressed calling Focus()) is actually exercised
    // rather than assumed.
    private static void Click(MainWindow window, Control control)
    {
        var centre = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var pointInWindow = control.TranslatePoint(centre, window)!.Value;
        window.MouseDown(pointInWindow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The focus-based panel-zoom hotkeys below depend on clicking a panel actually focusing it.
    /// PreviewControl (unlike TextureViewport) has no explicit <c>Focus()</c> call in its
    /// OnPointerPressed — this passes only because Avalonia auto-focuses a Focusable control on
    /// pointer press by default. Keep this test as a guard against that default ever being
    /// suppressed (e.g. a future PreviewControl change marking the press event Handled early).
    /// </summary>
    [AvaloniaFact]
    public void ClickingPreviewPanel_GivesItFocus()
    {
        var (window, _) = CreateWindow();
        try
        {
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;

            Click(window, preview);

            Assert.Same(preview, window.FocusManager?.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Ctrl+Plus/Minus targets whichever panel has focus rather than a fixed pane — clicking into
    /// wireframe then pressing Ctrl+Plus must zoom only wireframe, never preview.
    /// </summary>
    [AvaloniaFact]
    public void CtrlOemPlus_WireframeFocused_StepsWireframeZoomOnly()
    {
        var (window, _) = CreateWindow();
        try
        {
            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            Click(window, wireframe);

            window.KeyPress(Key.OemPlus, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1.5f, wireframe.Zoom);
            Assert.Equal(1f, preview.Zoom);
        }
        finally { window.Close(); }
    }

    /// <summary>The same gesture, but with focus on preview instead — zooms preview only.</summary>
    [AvaloniaFact]
    public void CtrlOemMinus_PreviewFocused_StepsPreviewZoomOnly()
    {
        var (window, _) = CreateWindow();
        try
        {
            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            Click(window, preview);

            window.KeyPress(Key.OemMinus, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0.75f, preview.Zoom);
            Assert.Equal(1f, wireframe.Zoom);
        }
        finally { window.Close(); }
    }

    /// <summary>Neither panel focused (tree has it) — Ctrl+Plus is a no-op, not a guess.</summary>
    [AvaloniaFact]
    public void CtrlOemPlus_NeitherPanelFocused_DoesNothing()
    {
        var (window, _) = CreateWindow();
        try
        {
            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            window.FindControl<TreeView>("AnimTree")!.Focus();

            window.KeyPress(Key.OemPlus, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1f, wireframe.Zoom);
            Assert.Equal(1f, preview.Zoom);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Ctrl+Shift+Plus/Minus is reserved, unclaimed, for a future app-wide zoom feature — it must
    /// NOT also trigger the focused panel's zoom (Forbidden:Shift on panel-zoom-in/out). This is
    /// the regression guard for the Gum tool's mistake: its app-wide and per-pane zoom hotkeys
    /// shared one gesture and were split apart only after a reported double-zoom bug.
    /// </summary>
    [AvaloniaFact]
    public void CtrlShiftOemPlus_WireframeFocused_DoesNotStepPanelZoom()
    {
        var (window, _) = CreateWindow();
        try
        {
            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            Click(window, wireframe);

            window.KeyPress(Key.OemPlus, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1f, wireframe.Zoom);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Menu InputGesture text for the #638 items now derives from the registry — Wireframe and
    /// Preview Zoom In both show the same gesture text since it's genuinely contextual.
    /// </summary>
    [AvaloniaFact]
    public void WireframeAndPreviewZoomInMenuItems_ShowTheSameSharedGesture()
    {
        var (window, _) = CreateWindow();
        try
        {
            var expected = new KeyGesture(Key.OemPlus, KeyModifiers.Control);

            Assert.Equal(expected, window.FindControl<MenuItem>("MenuWireframeZoomIn")!.InputGesture);
            Assert.Equal(expected, window.FindControl<MenuItem>("MenuPreviewZoomIn")!.InputGesture);
        }
        finally { window.Close(); }
    }
}
