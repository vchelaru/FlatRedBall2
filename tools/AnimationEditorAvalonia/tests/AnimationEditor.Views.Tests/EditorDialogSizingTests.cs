using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Views.Dialogs;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// #1037: several <see cref="EditorDialogs"/> factories built their window with a guessed fixed
/// <see cref="EditorDialogOptions.Height"/> instead of <see cref="EditorDialogOptions.SizeToContentHeight"/>,
/// leaving a large empty gap whenever the real content was shorter than the guess (most visibly the
/// Save/Don't Save/Cancel "Unsaved Changes" prompt). Every dialog should auto-size to its content instead.
/// </summary>
public class EditorDialogSizingTests
{
    private sealed class OptionsCapturingDialogHost : IEditorDialogHost
    {
        public EditorDialogOptions? CapturedOptions { get; private set; }

        public async Task<T> ShowAsync<T>(EditorDialog<T> dialog)
        {
            CapturedOptions = dialog.Options;
            dialog.Cancel();
            return await dialog.Result;
        }
    }

    private static void AssertAutoSized(EditorDialogOptions? options)
    {
        Assert.NotNull(options);
        Assert.True(options!.SizeToContentHeight);
        Assert.Null(options.Height);
    }

    [AvaloniaFact]
    public async Task ConfirmAsync_AutoSizesToContent()
    {
        var host = new OptionsCapturingDialogHost();
        await EditorDialogs.ConfirmAsync(host, "Continue?", "Confirm");
        AssertAutoSized(host.CapturedOptions);
    }

    [AvaloniaFact]
    public async Task ConfirmSaveDiscardCancelAsync_AutoSizesToContent()
    {
        var host = new OptionsCapturingDialogHost();
        await EditorDialogs.ConfirmSaveDiscardCancelAsync(
            host, "\"Untitled\" has unsaved changes. Save before closing?", "Unsaved Changes");
        AssertAutoSized(host.CapturedOptions);
    }

    [AvaloniaFact]
    public async Task PromptStringAsync_AutoSizesToContent()
    {
        var host = new OptionsCapturingDialogHost();
        await EditorDialogs.PromptStringAsync(host, "Rename", "New name:", "Walk");
        AssertAutoSized(host.CapturedOptions);
    }

    [AvaloniaFact]
    public async Task ShowAddMultipleFramesAsync_AutoSizesToContent()
    {
        var host = new OptionsCapturingDialogHost();
        await EditorDialogs.ShowAddMultipleFramesAsync(
            host, appCommands: null!, chain: new AnimationChainSave());
        AssertAutoSized(host.CapturedOptions);
    }

    [AvaloniaFact]
    public async Task ShowAdjustOffsetsAsync_AutoSizesToContent()
    {
        var host = new OptionsCapturingDialogHost();
        await EditorDialogs.ShowAdjustOffsetsAsync(
            host, appCommands: null!, chain: new AnimationChainSave(), getTextureHeight: _ => null);
        AssertAutoSized(host.CapturedOptions);
    }
}
