using AnimationEditor.Core.Sequences;

namespace AnimationEditor.Core.IO;

/// <summary>
/// What the editor's own Copy and Cut leave on the system clipboard as text: the sequence, chain and track envelopes
/// (<see cref="PasteRoute"/> reads those) and the <c>.achx</c> chain, frame and shape payloads
/// (<see cref="ClipboardPayload"/>). A name box refuses these so a stray Ctrl+V cannot turn into a name.
/// </summary>
public static class EditorClipboard
{
    /// <summary>
    /// True when <paramref name="text"/> is something the editor copied, including a copy that is damaged but still
    /// carries the marker. Ordinary text, other JSON and empty text are false.
    /// </summary>
    public static bool IsEditorEnvelope(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (ClipboardPayload.HasPayloadPrefix(text)) return true;
        var route = PasteRoute.For(text);
        return route.Kind != PasteRouteKind.Refused || route.IsDamaged;
    }
}
