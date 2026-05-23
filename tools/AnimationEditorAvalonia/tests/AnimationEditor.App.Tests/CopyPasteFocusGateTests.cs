using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #281: Ctrl+C / Ctrl+V / Delete must not be
/// swallowed by the window-level handler when a TextBox has keyboard focus.
/// Only when a non-text-editing surface is focused should those keystrokes
/// trigger frame/shape copy-paste or deletion.
/// </summary>
public class CopyPasteFocusGateTests
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

    /// <summary>
    /// Puts a serialized frame on the clipboard, focuses a TextBox, then presses
    /// Ctrl+V. The frame-paste handler must not fire — the chain stays empty.
    /// </summary>
    [AvaloniaFact]
    public async Task CtrlV_TextBoxFocused_DoesNotPasteFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var xml = ClipboardPayload.Serialize(
                new List<AnimationFrameSave> { new() { TextureName = "run.png", FrameLength = 0.1f } });
            await window.Clipboard!.SetTextAsync(xml);

            var speedInput = window.FindControl<TextBox>("SpeedInput")!;
            speedInput.Focus();
            Dispatcher.UIThread.RunJobs();

            // Verify focus is actually on a TextBox before pressing the key.
            // If focus is not on the TextBox, the gate cannot protect against paste.
            var focused = window.FocusManager?.GetFocusedElement();
            Assert.IsType<TextBox>(focused);

            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(chain.Frames);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Same setup, but focus is on the AnimTree (not a TextBox). Ctrl+V should
    /// trigger the paste handler and add the frame to the chain.
    /// </summary>
    [AvaloniaFact]
    public async Task CtrlV_NonTextBoxFocused_PastesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var xml = ClipboardPayload.Serialize(
                new List<AnimationFrameSave> { new() { TextureName = "run.png", FrameLength = 0.1f } });
            await window.Clipboard!.SetTextAsync(xml);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            tree.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Ctrl+C with a TextBox focused must not overwrite clipboard text with
    /// frame XML (nothing in the tree is being copied from a text-editor context).
    /// </summary>
    [AvaloniaFact]
    public async Task CtrlC_TextBoxFocused_DoesNotOverwriteClipboard()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Populate tree with a chain so HandleCopyAsync has something to serialize.
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            await window.Clipboard!.SetTextAsync("plain text");

            var speedInput = window.FindControl<TextBox>("SpeedInput")!;
            speedInput.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            var clipboardText = await window.Clipboard!.TryGetTextAsync();
            Assert.Equal("plain text", clipboardText);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// macOS regression: Meta+V (Cmd+V) must trigger the paste handler just like
    /// Ctrl+V does on Windows/Linux.
    /// </summary>
    [AvaloniaFact]
    public async Task MetaV_NonTextBoxFocused_PastesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var xml = ClipboardPayload.Serialize(
                new List<AnimationFrameSave> { new() { TextureName = "run.png", FrameLength = 0.1f } });
            await window.Clipboard!.SetTextAsync(xml);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            tree.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.V, RawInputModifiers.Meta, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// macOS regression: Meta+C (Cmd+C) must copy the selected chain to the
    /// clipboard, just like Ctrl+C does on Windows/Linux.
    /// </summary>
    [AvaloniaFact]
    public async Task MetaC_NonTextBoxFocused_CopiesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            // Directly seed the tree and select the chain node, matching the pattern
            // used by HeadlessTreeViewTests to avoid needing a full UI rebuild cycle.
            var tree = window.FindControl<TreeView>("AnimTree")!;
            var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
            var chainVm = new TreeNodeVm { Header = "Run", Data = chain };
            roots.Add(chainVm);
            tree.SelectedItems!.Add(chainVm);
            Dispatcher.UIThread.RunJobs();

            // Seed the clipboard with plain text so we can detect when it changes.
            await window.Clipboard!.SetTextAsync("unrelated");

            tree.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.C, RawInputModifiers.Meta, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            var clipboardText = await window.Clipboard!.TryGetTextAsync();
            Assert.NotEqual("unrelated", clipboardText);
            Assert.False(string.IsNullOrWhiteSpace(clipboardText));
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Delete with a TextBox focused must not trigger the frame/chain deletion
    /// handler. The chain with one frame must survive.
    /// </summary>
    [AvaloniaFact]
    public void Delete_TextBoxFocused_DoesNotDeleteSelectedFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "run.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = chain.Frames[0];

            var speedInput = window.FindControl<TextBox>("SpeedInput")!;
            speedInput.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
        }
        finally { window.Close(); }
    }
}
