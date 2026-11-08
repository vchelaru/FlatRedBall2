using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Every <see cref="TextBox"/> in the editor refuses to paste the editor's own copy JSON (a copied sequence, chain, track,
/// frame or shape, see <see cref="EditorClipboard"/>), so Ctrl+V or the context menu's Paste in a name box never turns
/// an item copy into a name. Ordinary text pastes as usual. The refusal is a class handler on
/// <see cref="TextBox.PastingFromClipboardEvent"/>, installed once when this assembly loads, so a TextBox added later
/// has it with no wiring. A box that should take any text (a search or free-text field) sets
/// <see cref="AllowEditorJsonProperty"/>; <c>NameBoxPasteScenarioTests</c> lists the boxes that do.
/// </summary>
public static class EditorTextPaste
{
    public static readonly AttachedProperty<bool> AllowEditorJsonProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("AllowEditorJson", typeof(EditorTextPaste));

    // Stopwatch timestamp until which the next PastingFromClipboard of a box is our own re-issued paste.
    private static readonly AttachedProperty<long> PasteApprovedUntilProperty =
        AvaloniaProperty.RegisterAttached<TextBox, long>("PasteApprovedUntil", typeof(EditorTextPaste));

    /// <summary>True when <paramref name="box"/> takes the editor's copy JSON like any other text.</summary>
    public static bool GetAllowEditorJson(TextBox box) => box.GetValue(AllowEditorJsonProperty);

    public static void SetAllowEditorJson(TextBox box, bool value) => box.SetValue(AllowEditorJsonProperty, value);

#pragma warning disable CA2255 // The refusal must exist before any TextBox does, in every host (the app and the test hosts).
    [ModuleInitializer]
    internal static void Install() =>
        TextBox.PastingFromClipboardEvent.AddClassHandler<TextBox>(OnPastingFromClipboard);
#pragma warning restore CA2255

    private static void OnPastingFromClipboard(TextBox box, RoutedEventArgs e)
    {
        if (GetAllowEditorJson(box)) return;
        long approvedUntil = box.GetValue(PasteApprovedUntilProperty);
        if (approvedUntil != 0 && Stopwatch.GetTimestamp() < approvedUntil)
        {
            box.SetValue(PasteApprovedUntilProperty, 0L);
            return;
        }
        // The handler is synchronous and cannot see the text, so cancel this paste, read the clipboard, and paste again only
        // when it holds ordinary text. The second paste is the box's own, so it behaves exactly as an unguarded one would.
        e.Handled = true;
        _ = PasteIfOrdinaryAsync(box);
    }

    private static async Task PasteIfOrdinaryAsync(TextBox box)
    {
        string? text = TopLevel.GetTopLevel(box)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;
        if (string.IsNullOrEmpty(text) || EditorClipboard.IsEditorEnvelope(text)) return;
        box.SetValue(PasteApprovedUntilProperty, Stopwatch.GetTimestamp() + 2 * Stopwatch.Frequency);
        box.Paste();
    }
}
