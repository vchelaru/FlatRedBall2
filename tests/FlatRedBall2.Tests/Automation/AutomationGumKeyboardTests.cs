using System;
using FlatRedBall2.Automation;
using Shouldly;
using Xunit;

using FrbKeyboard = FlatRedBall2.Input.Keyboard;
using GumKeys = Gum.Forms.Input.Keys;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;

namespace FlatRedBall2.Tests.Automation;

// --- The IInputReceiverKeyboard FRB2 hands to Gum while automation is active ---

public class AutomationGumKeyboardTests
{
    [Fact]
    public void GetStringTyped_AfterActivity_ReturnsQueuedTextThenIsEmptyNextFrame()
    {
        var gum = new AutomationGumKeyboard(new FrbKeyboard());

        gum.QueueText("ab");
        gum.Activity(0);
        gum.GetStringTyped().ShouldBe("ab");

        // Activity is the frame boundary, so text is delivered for exactly one frame — a second
        // delivery would type everything twice.
        gum.Activity(1);
        gum.GetStringTyped().ShouldBeEmpty();
    }

    [Fact]
    public void GetStringTyped_TwoQueuesBeforeOneActivity_ConcatenatesInOrder()
    {
        var gum = new AutomationGumKeyboard(new FrbKeyboard());

        gum.QueueText("ab");
        gum.QueueText("cd");
        gum.Activity(0);

        gum.GetStringTyped().ShouldBe("abcd");
    }

    [Fact]
    public void KeysTyped_KeyHeldFarPastRepeatDelay_ReportsThePressOnceAndNeverRepeats()
    {
        var frb = new FrbKeyboard();
        var gum = new AutomationGumKeyboard(frb);

        frb.InjectKey(XnaKeys.Back, down: true);
        frb.Update();
        gum.Activity(0);
        gum.KeysTyped.ShouldContain(GumKeys.Back);

        // Gum's own keyboard starts repeating after RepeatDelay (500ms). Automation must not:
        // repeat is wall-clock driven, so it would make a replay depend on how many frames the
        // driver happened to step. 120 frames is well past that delay at any frame rate.
        for (var frame = 1; frame <= 120; frame++)
        {
            frb.Update();
            gum.Activity(frame);
            gum.KeysTyped.ShouldBeEmpty();
        }
    }

    [Fact]
    public void GumKeysAndXnaKeys_ShareNamesAndNumericValues()
    {
        // AutomationGumKeyboard casts straight between the two enums. Gum documents that as a
        // permanent commitment (Gum.Forms.Input.Keys "mirrors Microsoft.Xna.Framework.Input.Keys
        // exactly"), so pin it — a silent divergence upstream would misroute every key.
        foreach (var xna in Enum.GetValues<XnaKeys>())
            Enum.GetName((GumKeys)(int)xna).ShouldBe(Enum.GetName(xna));
    }
}
