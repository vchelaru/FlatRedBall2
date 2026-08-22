using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Views.Dialogs;

/// <summary>Result of <see cref="EditorDialogs.ConfirmSaveDiscardCancelAsync"/>.</summary>
public enum SaveDiscardCancelChoice
{
    Save,
    Discard,
    Cancel,
}

public static class EditorDialogs
{
    private readonly record struct FrameTimeChoice(float TotalDuration, bool KeepProportional);
    private readonly record struct AddFramesChoice(int Count, bool IncrementUv);
    private readonly record struct OffsetChoice(bool JustifyBottom, float X, float Y, bool Relative);
    internal static EditorDialogOptions AdjustFrameTimeOptions =>
        new("Adjust All Frame Time", 360, SizeToContentHeight: true);

    public static Task<bool> ConfirmAsync(
        IEditorDialogHost host, string message, string title,
        string confirmLabel = "Yes", string cancelLabel = "No")
    {
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var dialog = new EditorDialog<bool>(
            new EditorDialogOptions(title, 420, 160), panel, cancelResult: false);
        dialog.Confirm = () => dialog.Complete(true);
        dialog.Cancel = () => dialog.Complete(false);
        panel.Children.Add(BuildButtonRow(
            (confirmLabel, dialog.Confirm),
            (cancelLabel, dialog.Cancel)));
        return host.ShowAsync(dialog);
    }

    /// <summary>
    /// Three-way Save / Don't Save / Cancel prompt (e.g. closing a tab with unsaved changes).
    /// Enter triggers Save, Escape (or the window's close button) triggers Cancel.
    /// </summary>
    public static Task<SaveDiscardCancelChoice> ConfirmSaveDiscardCancelAsync(
        IEditorDialogHost host, string message, string title,
        string saveLabel = "Save", string discardLabel = "Don't Save", string cancelLabel = "Cancel")
    {
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var dialog = new EditorDialog<SaveDiscardCancelChoice>(
            new EditorDialogOptions(title, 420, 160), panel, cancelResult: SaveDiscardCancelChoice.Cancel);
        dialog.Confirm = () => dialog.Complete(SaveDiscardCancelChoice.Save);
        dialog.Cancel = () => dialog.Complete(SaveDiscardCancelChoice.Cancel);
        panel.Children.Add(BuildButtonRow(
            (saveLabel, dialog.Confirm),
            (discardLabel, () => dialog.Complete(SaveDiscardCancelChoice.Discard)),
            (cancelLabel, dialog.Cancel)));
        return host.ShowAsync(dialog);
    }

    public static Task<string?> PromptStringAsync(
        IEditorDialogHost host, string title, string prompt, string initial = "")
    {
        var input = new TextBox { Text = initial };
        var panel = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(input);

        var dialog = new EditorDialog<string?>(
            new EditorDialogOptions(title, 380, 155), panel, cancelResult: null);
        dialog.Confirm = () => dialog.Complete(input.Text);
        dialog.Cancel = () => dialog.Complete(null);
        panel.Children.Add(BuildButtonRow(
            ("OK", dialog.Confirm),
            ("Cancel", dialog.Cancel)));

        input.AttachedToVisualTree += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        return host.ShowAsync(dialog);
    }

    /// <summary>
    /// Every field edit applies immediately (so the preview stays in sync while the dialog is
    /// open) instead of waiting for OK. Cancel — via its button, Escape, or the window's close
    /// button, all of which resolve to a null choice — discards those live edits with
    /// <see cref="IAppCommands.DiscardPendingEdits"/>; OK seals them into a normal undo entry
    /// with <see cref="IAppCommands.SealPendingEdits"/>.
    /// </summary>
    public static async Task ShowAdjustFrameTimeAsync(
        IEditorDialogHost host, IAppCommands appCommands, AnimationChainSave chain)
    {
        if (chain.Frames.Count == 0) return;

        int frameCount = chain.Frames.Count;
        float totalDuration = chain.Frames.Sum(f => f.FrameLength);
        bool canProportional = totalDuration > 0f;

        // Original per-frame lengths, captured once. Live edits always scale from this snapshot
        // (not from whatever the previous live edit left behind) so switching between "Keep
        // Proportional" and "Set All Frames Same" mid-dialog can't flatten the original ratios.
        var originalLengths = chain.Frames.Select(f => f.FrameLength).ToArray();
        bool didEdit = false;

        var durationInput = new FlankerNumericField
        {
            Value = (decimal)totalDuration,
            Minimum = 0m,
            Maximum = 3600m,
            Increment = 0.1m,
            FormatString = "0.000",
            Width = 160,
        };
        var radioProportional = new RadioButton
        {
            Content = "Keep Proportional",
            IsChecked = canProportional,
            IsEnabled = canProportional,
        };
        var radioSetAll = new RadioButton
        {
            Content = "Set All Frames Same",
            IsChecked = !canProportional,
        };
        var perFrameLabel = new TextBlock { FontSize = 11, Foreground = Brushes.Gray };

        void UpdateLabel()
        {
            float value = (float)(durationInput.Value ?? 0m);
            perFrameLabel.IsVisible = radioSetAll.IsChecked == true;
            if (perFrameLabel.IsVisible)
                perFrameLabel.Text = $"Each frame: {value / frameCount:F3} seconds";
        }

        void ApplyLive()
        {
            for (int i = 0; i < chain.Frames.Count; i++)
                chain.Frames[i].FrameLength = originalLengths[i];
            didEdit = true;
            float value = (float)(durationInput.Value ?? 0m);
            if (radioProportional.IsChecked == true)
                appCommands.ScaleFrameTimesProportional(chain, value);
            else
                appCommands.ScaleFrameTimesSetAllSame(chain, value);
        }

        durationInput.ValueChanged += (_, _) => { UpdateLabel(); ApplyLive(); };
        radioSetAll.IsCheckedChanged += (_, _) => { UpdateLabel(); if (radioSetAll.IsChecked == true) ApplyLive(); };
        radioProportional.IsCheckedChanged += (_, _) => { UpdateLabel(); if (radioProportional.IsChecked == true) ApplyLive(); };
        UpdateLabel();

        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Total animation duration (seconds):" });
        panel.Children.Add(durationInput);
        panel.Children.Add(radioProportional);
        panel.Children.Add(radioSetAll);
        panel.Children.Add(perFrameLabel);

        var dialog = new EditorDialog<FrameTimeChoice?>(
            AdjustFrameTimeOptions, panel, cancelResult: null);
        dialog.Confirm = () => dialog.Complete(new FrameTimeChoice(
            (float)(durationInput.Value ?? 0m), radioProportional.IsChecked == true));
        dialog.Cancel = () => dialog.Complete(null);
        panel.Children.Add(BuildButtonRow(("OK", dialog.Confirm), ("Cancel", dialog.Cancel)));

        var choice = await host.ShowAsync(dialog);
        if (choice is null)
        {
            if (didEdit)
                appCommands.DiscardPendingEdits();
            return;
        }
        appCommands.SealPendingEdits();
    }

    public static async Task ShowAddMultipleFramesAsync(
        IEditorDialogHost host, IAppCommands appCommands, AnimationChainSave chain,
        Action<string>? showStatus = null)
    {
        var countInput = new NumericUpDown
        {
            Value = 1,
            Minimum = 1,
            Maximum = 1000,
            Increment = 1,
            FormatString = "0",
            Width = 100,
        };
        var incrementToggle = new CheckBox { Content = "Increment UV", IsChecked = true };
        var panel = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Number of frames to add:" });
        panel.Children.Add(countInput);
        panel.Children.Add(incrementToggle);

        var dialog = new EditorDialog<AddFramesChoice?>(
            new EditorDialogOptions("Add Multiple Frames", 320, 200), panel, cancelResult: null);
        dialog.Confirm = () => dialog.Complete(new AddFramesChoice(
            (int)(countInput.Value ?? 0), incrementToggle.IsChecked == true));
        dialog.Cancel = () => dialog.Complete(null);
        panel.Children.Add(BuildButtonRow(("OK", dialog.Confirm), ("Cancel", dialog.Cancel)));

        var choice = await host.ShowAsync(dialog);
        if (choice is not { Count: > 0 } result) return;
        if (appCommands.AddMultipleFrames(chain, result.Count, result.IncrementUv))
            showStatus?.Invoke("Some frames were clipped — exceeded texture bounds.");
    }

    public static async Task ShowAdjustOffsetsAsync(
        IEditorDialogHost host, IAppCommands appCommands, AnimationChainSave chain,
        Func<AnimationFrameSave, float?> getTextureHeight)
    {
        var justifyBottom = new RadioButton
        {
            Content = "Justify Bottom",
            IsChecked = true,
            GroupName = "mode",
        };
        var adjustAll = new RadioButton
        {
            Content = "Adjust All (enter values)",
            GroupName = "mode",
        };
        var (adjustAllRow, xInput, yInput) = BuildAdjustAllRow();
        var absolute = new RadioButton
        {
            Content = "Absolute",
            IsChecked = true,
            GroupName = "offsetMode",
        };
        var relative = new RadioButton { Content = "Relative", GroupName = "offsetMode" };
        var offsetModeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
        };
        offsetModeRow.Children.Add(absolute);
        offsetModeRow.Children.Add(relative);

        void UpdateModeVisibility()
        {
            adjustAllRow.IsVisible = adjustAll.IsChecked == true;
            offsetModeRow.IsVisible = adjustAll.IsChecked == true;
        }

        adjustAll.IsCheckedChanged += (_, _) => UpdateModeVisibility();
        UpdateModeVisibility();

        var panel = new StackPanel { Margin = new Thickness(14), Spacing = 8 };
        panel.Children.Add(justifyBottom);
        panel.Children.Add(adjustAll);
        panel.Children.Add(adjustAllRow);
        panel.Children.Add(offsetModeRow);

        var dialog = new EditorDialog<OffsetChoice?>(
            new EditorDialogOptions("Adjust Offsets", 340, 265), panel, cancelResult: null);
        dialog.Confirm = () => dialog.Complete(new OffsetChoice(
            justifyBottom.IsChecked == true,
            (float)(xInput.Value ?? 0),
            (float)(yInput.Value ?? 0),
            relative.IsChecked == true));
        dialog.Cancel = () => dialog.Complete(null);
        panel.Children.Add(BuildButtonRow(("OK", dialog.Confirm), ("Cancel", dialog.Cancel)));

        var choice = await host.ShowAsync(dialog);
        if (choice is not { } result) return;
        if (result.JustifyBottom)
            appCommands.AdjustOffsetsJustifyBottom(chain, getTextureHeight);
        else
            appCommands.AdjustOffsetsAdjustAll(chain, result.X, result.Y, result.Relative);
    }

    internal static (Grid AdjustAllRow, NumericUpDown XInput, NumericUpDown YInput) BuildAdjustAllRow()
    {
        var xInput = new NumericUpDown
        {
            Value = 0,
            FormatString = "0.###",
            Minimum = -9999,
            Maximum = 9999,
        };
        var yInput = new NumericUpDown
        {
            Value = 0,
            FormatString = "0.###",
            Minimum = -9999,
            Maximum = 9999,
        };
        var xLabel = new TextBlock
        {
            Text = "X:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        var yLabel = new TextBlock
        {
            Text = "Y:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0),
        };

        Grid.SetColumn(xLabel, 0);
        Grid.SetColumn(xInput, 1);
        Grid.SetColumn(yLabel, 2);
        Grid.SetColumn(yInput, 3);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*") };
        grid.Children.Add(xLabel);
        grid.Children.Add(xInput);
        grid.Children.Add(yLabel);
        grid.Children.Add(yInput);
        return (grid, xInput, yInput);
    }

    private static StackPanel BuildButtonRow(params (string Label, Action Click)[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        foreach (var (label, click) in buttons)
        {
            var button = new Button { Content = label };
            button.Click += (_, _) => click();
            row.Children.Add(button);
        }
        return row;
    }
}
