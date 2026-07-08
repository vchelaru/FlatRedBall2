using AnimationEditor.Core.Hotkeys;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void Matches_RequiredModifierPresentPlusUnrelatedModifier_ReturnsTrue()
    {
        // Ctrl+Shift+C still triggers Copy — Shift is neither required nor forbidden.
        var gesture = new HotkeyGesture("C", Required: HotkeyModifiers.Command);

        bool result = gesture.Matches("C", HotkeyModifiers.Command | HotkeyModifiers.Shift);

        Assert.True(result);
    }

    [Fact]
    public void Matches_ForbiddenModifierPresent_ReturnsFalse()
    {
        // Ctrl+Shift+Z must not match Undo, since Undo forbids Shift (it's Redo's gesture).
        var gesture = new HotkeyGesture("Z", Required: HotkeyModifiers.Command, Forbidden: HotkeyModifiers.Shift);

        bool result = gesture.Matches("Z", HotkeyModifiers.Command | HotkeyModifiers.Shift);

        Assert.False(result);
    }

    [Fact]
    public void Matches_KeyNameDiffers_ReturnsFalse()
    {
        var gesture = new HotkeyGesture("C", Required: HotkeyModifiers.Command);

        bool result = gesture.Matches("V", HotkeyModifiers.Command);

        Assert.False(result);
    }

    [Fact]
    public void Matches_RequiredModifierMissing_ReturnsFalse()
    {
        var gesture = new HotkeyGesture("C", Required: HotkeyModifiers.Command);

        bool result = gesture.Matches("C", HotkeyModifiers.None);

        Assert.False(result);
    }
}
