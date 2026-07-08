using AnimationEditor.Core.Hotkeys;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HotkeyDefinitionTests
{
    [Fact]
    public void DisplayText_CommandOnlyGesture_ReturnsCtrlPrefixedText()
    {
        var hotkey = new HotkeyDefinition
        {
            Id = "undo",
            Description = "Undo",
            Category = "Edit",
            Gestures = new[] { new HotkeyGesture("Z", HotkeyModifiers.Command, Forbidden: HotkeyModifiers.Shift) },
            Action = () => { },
        };

        Assert.Equal("Ctrl+Z", hotkey.DisplayText);
    }

    [Fact]
    public void DisplayText_MultipleGestures_UsesFirstGestureOnly()
    {
        // Redo displays "Ctrl+Y" on the menu even though Ctrl+Shift+Z also triggers it.
        var hotkey = new HotkeyDefinition
        {
            Id = "redo",
            Description = "Redo",
            Category = "Edit",
            Gestures = new[]
            {
                new HotkeyGesture("Y", HotkeyModifiers.Command),
                new HotkeyGesture("Z", HotkeyModifiers.Command | HotkeyModifiers.Shift, Forbidden: HotkeyModifiers.Alt),
            },
            Action = () => { },
        };

        Assert.Equal("Ctrl+Y", hotkey.DisplayText);
    }

    [Fact]
    public void DisplayText_NoModifiers_ReturnsKeyNameOnly()
    {
        var hotkey = new HotkeyDefinition
        {
            Id = "toggle-diagnostics",
            Description = "Toggle Render Diagnostics",
            Category = "View",
            Gestures = new[] { new HotkeyGesture("F3") },
            Action = () => { },
        };

        Assert.Equal("F3", hotkey.DisplayText);
    }
}
