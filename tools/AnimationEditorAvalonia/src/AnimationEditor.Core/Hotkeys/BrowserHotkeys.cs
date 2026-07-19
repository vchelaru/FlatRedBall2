using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Hotkeys;

/// <summary>
/// Filters a hotkey table down to the subset safe to dispatch in a browser-hosted build.
/// <see cref="ReservedIds"/> are ids the desktop table (<c>MainWindow.BuildHotkeyDefinitions</c>)
/// defines whose gesture is hard-reserved by browsers before the page ever sees the keypress —
/// Ctrl+N (new window), Ctrl+L (address bar), Ctrl+D (bookmark), Ctrl+Plus/Minus (page zoom).
/// </summary>
public static class BrowserHotkeys
{
    public static readonly IReadOnlyCollection<string> ReservedIds = new HashSet<string>
    {
        "new", "load", "duplicate", "panel-zoom-in", "panel-zoom-out",
    };

    /// <summary>Removes any definition whose <see cref="HotkeyDefinition.Id"/> is browser-reserved.</summary>
    public static IReadOnlyList<HotkeyDefinition> Filter(IReadOnlyList<HotkeyDefinition> hotkeys) =>
        hotkeys.Where(h => !ReservedIds.Contains(h.Id)).ToList();
}
