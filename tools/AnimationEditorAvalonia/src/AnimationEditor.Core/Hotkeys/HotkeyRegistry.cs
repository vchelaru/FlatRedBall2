using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Hotkeys;

/// <summary>
/// Pure dispatch logic over a list of <see cref="HotkeyDefinition"/>s. Kept UI-independent so
/// "does this keypress match this registry entry" is unit-testable without a window.
/// </summary>
public static class HotkeyRegistry
{
    /// <summary>
    /// Returns the first definition whose gestures match, or <c>null</c>. Definitions are
    /// checked in list order — first-match-wins, mirroring the original if/else chain.
    /// </summary>
    public static HotkeyDefinition? FindMatch(
        IEnumerable<HotkeyDefinition> hotkeys, string keyName, HotkeyModifiers pressed) =>
        hotkeys.FirstOrDefault(h => h.Matches(keyName, pressed));

    /// <summary>
    /// Gestures shared by two different definitions — a sign one hotkey would silently shadow
    /// the other, since only the first-listed definition would ever fire for that keypress.
    /// </summary>
    public static IReadOnlyList<(HotkeyGesture Gesture, string FirstId, string SecondId)> FindDuplicateGestures(
        IReadOnlyList<HotkeyDefinition> hotkeys)
    {
        var duplicates = new List<(HotkeyGesture, string, string)>();
        for (int i = 0; i < hotkeys.Count; i++)
            for (int j = i + 1; j < hotkeys.Count; j++)
                foreach (var gesture in hotkeys[i].Gestures)
                    if (hotkeys[j].Gestures.Contains(gesture))
                        duplicates.Add((gesture, hotkeys[i].Id, hotkeys[j].Id));
        return duplicates;
    }
}
