using AnimationEditor.Core.IO;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Answers, ahead of time, the dialogs the editor opens through its delegate seams
/// (<c>AppCommands.ConfirmAsync</c>, <c>PromptStringAsync</c>, <c>FileDialogService</c> and the
/// window's Save / Don't Save / Cancel prompt), the way a user would fill each one in. A dialog
/// nothing answered is recorded in <see cref="Unanswered"/> and fails the scenario at the next
/// <c>Layout()</c>, instead of hanging the headless run or vanishing into a guarded action's
/// error banner.
/// </summary>
internal sealed class ScriptedDialogs : IFileDialogService, IEditorDialogHost
{
    private readonly Queue<Func<Control, bool>> _editorDialogs = new();
    private readonly Queue<bool> _confirms = new();
    private readonly Queue<string?> _prompts = new();
    private readonly Queue<SaveDiscardCancelChoice> _saveDiscardCancels = new();
    private readonly Queue<string?> _saveFiles = new();
    private readonly Queue<string?> _openFiles = new();

    /// <summary>Every dialog the editor opened, in order, as "kind: title / message".</summary>
    public List<string> Shown { get; } = new();

    /// <summary>Dialogs that opened with no queued answer.</summary>
    public List<string> Unanswered { get; } = new();

    public void AnswerNextConfirm(bool ok) => _confirms.Enqueue(ok);

    /// <summary>The text typed into the next string prompt, or null for Cancel.</summary>
    public void AnswerNextPrompt(string? text) => _prompts.Enqueue(text);

    public void AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice choice) => _saveDiscardCancels.Enqueue(choice);

    /// <summary>The path picked in the next save-file dialog, or null for Cancel.</summary>
    public void AnswerNextSaveFile(string? path) => _saveFiles.Enqueue(path);

    /// <summary>The path picked in the next open-file dialog, or null for Cancel.</summary>
    public void AnswerNextOpenFile(string? path) => _openFiles.Enqueue(path);

    /// <summary>
    /// Fills in the next dialog that opens through <see cref="IEditorDialogHost"/> (Adjust Frame
    /// Time, Add Multiple Frames, Adjust Offsets): <paramref name="fill"/> gets the dialog's
    /// content, mounted in a headless window so its controls work, and returns true for OK or
    /// false for Cancel.
    /// </summary>
    public void AnswerNextEditorDialog(Func<Control, bool> fill) => _editorDialogs.Enqueue(fill);

    /// <inheritdoc/>
    public async Task<T> ShowAsync<T>(EditorDialog<T> dialog)
    {
        Func<Control, bool> fill = Take(_editorDialogs, $"editor dialog: {dialog.Options.Title}");
        Window window = new Window { Width = dialog.Options.Width, Height = dialog.Options.Height ?? 400, Content = dialog.Content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        try
        {
            if (fill(dialog.Content))
            {
                dialog.Confirm();
            }
            else
            {
                dialog.Cancel();
            }
            Dispatcher.UIThread.RunJobs();
            return await dialog.Result;
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    public Task<bool> ConfirmAsync(string message, string title) =>
        Task.FromResult(Take(_confirms, $"confirm: {title} / {message}"));

    public Task<string?> PromptStringAsync(string title, string prompt, string initial) =>
        Task.FromResult(Take(_prompts, $"prompt: {title} / {prompt} (initial \"{initial}\")"));

    public Task<SaveDiscardCancelChoice> SaveDiscardCancelAsync(string message, string title) =>
        Task.FromResult(Take(_saveDiscardCancels, $"save-discard-cancel: {title} / {message}"));

    public Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices) =>
        Task.FromResult(Take(_saveFiles, $"save file: {title} (.{defaultExtension})"));

    public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription) =>
        Task.FromResult(Take(_openFiles, $"open file: {title} (.{defaultExtension})"));

    /// <summary>Fails when a dialog opened that the scenario had not answered.</summary>
    public void ThrowIfUnanswered()
    {
        if (Unanswered.Count > 0)
        {
            throw new InvalidOperationException(
                "The editor opened a dialog the scenario did not answer: " + string.Join("; ", Unanswered));
        }
    }

    private T Take<T>(Queue<T> answers, string description)
    {
        Shown.Add(description);
        if (answers.Count == 0)
        {
            Unanswered.Add(description);
            throw new InvalidOperationException($"No answer was queued for the dialog \"{description}\".");
        }
        return answers.Dequeue();
    }
}
