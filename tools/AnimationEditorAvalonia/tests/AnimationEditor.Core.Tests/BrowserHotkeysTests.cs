using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Hotkeys;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class BrowserHotkeysTests
{
    private static HotkeyDefinition MakeDefinition(string id) =>
        new()
        {
            Id = id,
            Description = id,
            Category = "Edit",
            Gestures = new[] { new HotkeyGesture(id) },
            Action = () => { },
        };

    [Fact]
    public void Filter_FullDesktopTable_ExcludesReservedIdsAndKeepsRest()
    {
        var hotkeys = new List<HotkeyDefinition>
        {
            MakeDefinition("save"),
            MakeDefinition("undo"),
            MakeDefinition("redo"),
            MakeDefinition("toggle-diagnostics"),
            MakeDefinition("new"),
            MakeDefinition("load"),
            MakeDefinition("duplicate"),
            MakeDefinition("panel-zoom-in"),
            MakeDefinition("panel-zoom-out"),
        };

        var filtered = BrowserHotkeys.Filter(hotkeys);

        Assert.Equal(
            new[] { "save", "undo", "redo", "toggle-diagnostics" },
            filtered.Select(h => h.Id));
    }
}
