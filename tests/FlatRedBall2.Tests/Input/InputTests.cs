using System;
using System.Collections.Generic;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

using FrbKeyboard = FlatRedBall2.Input.Keyboard;

namespace FlatRedBall2.Tests.Input;

// --- fakes ---

file sealed class FakeKeyboard : IKeyboard
{
    public bool KeyDown { get; set; }
    public bool KeyPressed { get; set; }
    public bool KeyReleased { get; set; }

    public bool IsKeyDown(Keys key) => KeyDown;
    public bool WasKeyPressed(Keys key) => KeyPressed;
    public bool WasKeyJustReleased(Keys key) => KeyReleased;
}

file sealed class FakeGamepad : IGamepad
{
    public bool ButtonDown { get; set; }
    public bool ButtonJustPressed { get; set; }
    public bool ButtonJustReleased { get; set; }

    public bool IsButtonDown(Buttons button) => ButtonDown;
    public bool WasButtonJustPressed(Buttons button) => ButtonJustPressed;
    public bool WasButtonJustReleased(Buttons button) => ButtonJustReleased;
    public float GetAxis(GamepadAxis axis) => 0f;
}

// --- Cursor ---

public class CursorTests
{
    private static MouseState Mouse(ButtonState left = ButtonState.Released, ButtonState right = ButtonState.Released) =>
        new MouseState(
            x: 0, y: 0, scrollWheel: 0,
            leftButton: left,
            middleButton: ButtonState.Released,
            rightButton: right,
            xButton1: ButtonState.Released,
            xButton2: ButtonState.Released);

    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void PrimaryClick_TransitionDownToUp_ReturnsTrue()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(left: ButtonState.Pressed), Sec(0));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.01));

        cursor.PrimaryClick.ShouldBeTrue();
    }

    [Fact]
    public void PrimaryDoubleClick_TwoReleasesWithinThreshold_ReturnsTrue()
    {
        var cursor = new Cursor { DoubleClickThreshold = Sec(0.25) };

        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.00));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.05));  // click 1
        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.10));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.20));  // click 2 within threshold of click 1

        cursor.PrimaryDoubleClick.ShouldBeTrue();
    }

    [Fact]
    public void PrimaryDoubleClick_TwoReleasesBeyondThreshold_ReturnsFalse()
    {
        var cursor = new Cursor { DoubleClickThreshold = Sec(0.25) };

        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.00));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.05));
        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.40));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.50));  // gap 0.45 > 0.25

        cursor.PrimaryDoubleClick.ShouldBeFalse();
    }

    [Fact]
    public void PrimaryDoublePressed_TwoPressesWithinThreshold_ReturnsTrue()
    {
        var cursor = new Cursor { DoubleClickThreshold = Sec(0.25) };

        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.00));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.05));
        cursor.Update(Mouse(left: ButtonState.Pressed),  Sec(0.10));  // press 2 within threshold

        cursor.PrimaryDoublePressed.ShouldBeTrue();
    }

    [Fact]
    public void SecondaryClick_TransitionDownToUp_ReturnsTrue()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0));
        cursor.Update(Mouse(right: ButtonState.Released), Sec(0.01));

        cursor.SecondaryClick.ShouldBeTrue();
    }

    [Fact]
    public void SecondaryDoubleClick_TwoReleasesWithinThreshold_ReturnsTrue()
    {
        var cursor = new Cursor { DoubleClickThreshold = Sec(0.25) };

        cursor.Update(Mouse(right: ButtonState.Pressed),  Sec(0.00));
        cursor.Update(Mouse(right: ButtonState.Released), Sec(0.05));
        cursor.Update(Mouse(right: ButtonState.Pressed),  Sec(0.10));
        cursor.Update(Mouse(right: ButtonState.Released), Sec(0.20));

        cursor.SecondaryDoubleClick.ShouldBeTrue();
    }

    [Fact]
    public void SecondaryDown_RightMouseHeld_ReturnsTrue()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0));

        cursor.SecondaryDown.ShouldBeTrue();
    }

    [Fact]
    public void SecondaryPressed_HeldAcrossTwoFrames_ReturnsFalse()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0));
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0.01));

        cursor.SecondaryPressed.ShouldBeFalse();
    }

    [Fact]
    public void SecondaryPressed_TransitionUpToDown_ReturnsTrue()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(right: ButtonState.Released), Sec(0));
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0.01));

        cursor.SecondaryPressed.ShouldBeTrue();
    }

    // --- SuppressHeldReleases (screen-transition carryover) ---

    [Fact]
    public void PrimaryClick_ReleaseAfterSuppressHeldReleases_ReturnsFalse()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(left: ButtonState.Pressed), Sec(0));

        // Simulates a screen transition while the button is still held: the eventual release
        // must not register as a click belonging to whatever screen is active when it happens.
        cursor.SuppressHeldReleases();

        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.01));

        cursor.PrimaryClick.ShouldBeFalse();
    }

    [Fact]
    public void PrimaryClick_FreshPressAfterSuppressedRelease_StillFiresNormally()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(left: ButtonState.Pressed), Sec(0));
        cursor.SuppressHeldReleases();
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.01)); // suppressed, and consumed

        cursor.Update(Mouse(left: ButtonState.Pressed), Sec(0.02));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.03));

        cursor.PrimaryClick.ShouldBeTrue();
    }

    [Fact]
    public void PrimaryClick_PressAfterSuppressHeldReleases_StillFiresNormally()
    {
        // Nothing was held at the moment of the transition, so suppression should be a no-op.
        var cursor = new Cursor();
        cursor.Update(Mouse(), Sec(0));

        cursor.SuppressHeldReleases();

        cursor.Update(Mouse(left: ButtonState.Pressed), Sec(0.01));
        cursor.Update(Mouse(left: ButtonState.Released), Sec(0.02));

        cursor.PrimaryClick.ShouldBeTrue();
    }

    [Fact]
    public void SecondaryClick_ReleaseAfterSuppressHeldReleases_ReturnsFalse()
    {
        var cursor = new Cursor();
        cursor.Update(Mouse(right: ButtonState.Pressed), Sec(0));

        cursor.SuppressHeldReleases();

        cursor.Update(Mouse(right: ButtonState.Released), Sec(0.01));

        cursor.SecondaryClick.ShouldBeFalse();
    }
}

// --- Keyboard ---

public class KeyboardReleaseSuppressionTests
{
    [Fact]
    public void WasKeyJustReleased_ReleaseAfterSuppressHeldReleases_ReturnsFalse()
    {
        var keyboard = new FrbKeyboard();
        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();

        keyboard.SuppressHeldReleases();

        keyboard.InjectKey(Keys.Space, down: false);
        keyboard.Update();

        keyboard.WasKeyJustReleased(Keys.Space).ShouldBeFalse();
    }

    [Fact]
    public void WasKeyJustReleased_FreshPressReleaseAfterSuppression_StillFiresNormally()
    {
        var keyboard = new FrbKeyboard();
        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();
        keyboard.SuppressHeldReleases();
        keyboard.InjectKey(Keys.Space, down: false);
        keyboard.Update(); // suppressed, and consumed

        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();
        keyboard.InjectKey(Keys.Space, down: false);
        keyboard.Update();

        keyboard.WasKeyJustReleased(Keys.Space).ShouldBeTrue();
    }

    [Fact]
    public void WasKeyJustReleased_KeyNotHeldAtSuppression_StillFiresNormally()
    {
        var keyboard = new FrbKeyboard();
        keyboard.Update(); // nothing held

        keyboard.SuppressHeldReleases();

        keyboard.InjectKey(Keys.Enter, down: true);
        keyboard.Update();
        keyboard.InjectKey(Keys.Enter, down: false);
        keyboard.Update();

        keyboard.WasKeyJustReleased(Keys.Enter).ShouldBeTrue();
    }
}

// --- Gamepad ---

public class GamepadReleaseSuppressionTests
{
    [Fact]
    public void WasButtonJustReleased_ReleaseAfterSuppressHeldReleases_ReturnsFalse()
    {
        var gamepad = new Gamepad(0);
        gamepad.InjectButton(Buttons.A, down: true);
        gamepad.Update();

        gamepad.SuppressHeldReleases();

        gamepad.InjectButton(Buttons.A, down: false);
        gamepad.Update();

        gamepad.WasButtonJustReleased(Buttons.A).ShouldBeFalse();
    }

    [Fact]
    public void WasButtonJustReleased_FreshPressReleaseAfterSuppression_StillFiresNormally()
    {
        var gamepad = new Gamepad(0);
        gamepad.InjectButton(Buttons.A, down: true);
        gamepad.Update();
        gamepad.SuppressHeldReleases();
        gamepad.InjectButton(Buttons.A, down: false);
        gamepad.Update(); // suppressed, and consumed

        gamepad.InjectButton(Buttons.A, down: true);
        gamepad.Update();
        gamepad.InjectButton(Buttons.A, down: false);
        gamepad.Update();

        gamepad.WasButtonJustReleased(Buttons.A).ShouldBeTrue();
    }
}

// --- InputManager.SuppressHeldReleases forwarding ---

public class InputManagerSuppressHeldReleasesTests
{
    [Fact]
    public void SuppressHeldReleases_ForwardsToKeyboardCursorAndGamepads()
    {
        var manager = new InputManager();
        manager.InjectKey(Keys.Space, down: true);
        manager.InjectCursor(0, 0, primary: true, secondary: false);
        manager.InjectGamepadButton(0, Buttons.A, down: true);
        manager.Update(TimeSpan.Zero);

        manager.SuppressHeldReleases();

        manager.InjectKey(Keys.Space, down: false);
        manager.InjectCursor(0, 0, primary: false, secondary: false);
        manager.InjectGamepadButton(0, Buttons.A, down: false);
        manager.Update(TimeSpan.FromSeconds(0.01));

        manager.Keyboard.WasKeyJustReleased(Keys.Space).ShouldBeFalse();
        manager.Cursor.PrimaryClick.ShouldBeFalse();
        manager.GetGamepad(0).WasButtonJustReleased(Buttons.A).ShouldBeFalse();
    }
}

// --- KeyboardPressableInput ---

public class KeyboardPressableInputTests
{
    [Fact]
    public void WasJustPressed_DelegatesToWasKeyPressed()
    {
        var fake = new FakeKeyboard { KeyPressed = true };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        input.WasJustPressed.ShouldBeTrue();
    }

    [Fact]
    public void WasJustReleased_DelegatesToWasKeyJustReleased()
    {
        var fake = new FakeKeyboard { KeyReleased = true };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        input.WasJustReleased.ShouldBeTrue();
    }

    [Fact]
    public void WasJustReleased_ReturnsFalse_WhenNotReleased()
    {
        var fake = new FakeKeyboard { KeyReleased = false };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        input.WasJustReleased.ShouldBeFalse();
    }
}

// --- GamepadPressableInput ---

public class GamepadPressableInputTests
{
    [Fact]
    public void WasJustPressed_DelegatesToWasButtonJustPressed()
    {
        var fake = new FakeGamepad { ButtonJustPressed = true };
        var input = new GamepadPressableInput(fake, Buttons.A);

        input.WasJustPressed.ShouldBeTrue();
    }

    [Fact]
    public void WasJustPressed_ReturnsFalse_WhenNotPressed()
    {
        var fake = new FakeGamepad { ButtonJustPressed = false };
        var input = new GamepadPressableInput(fake, Buttons.A);

        input.WasJustPressed.ShouldBeFalse();
    }

    [Fact]
    public void WasJustReleased_DelegatesToWasButtonJustReleased()
    {
        var fake = new FakeGamepad { ButtonJustReleased = true };
        var input = new GamepadPressableInput(fake, Buttons.A);

        input.WasJustReleased.ShouldBeTrue();
    }
}

// --- GamepadDPadInput2D ---

file sealed class FakeDPadGamepad : IGamepad
{
    private readonly HashSet<Buttons> _down = new();

    public void Hold(Buttons button) => _down.Add(button);

    public bool IsButtonDown(Buttons button) => _down.Contains(button);
    public bool WasButtonJustPressed(Buttons button) => false;
    public bool WasButtonJustReleased(Buttons button) => false;
    public float GetAxis(GamepadAxis axis) => 0f;
}

public class GamepadDPadInput2DTests
{
    [Fact]
    public void X_DPadLeftHeld_ReturnsNegativeOne()
    {
        var fake = new FakeDPadGamepad();
        fake.Hold(Buttons.DPadLeft);
        var input = new GamepadDPadInput2D(fake);

        input.X.ShouldBe(-1f);
    }

    [Fact]
    public void X_DPadRightHeld_ReturnsPositiveOne()
    {
        var fake = new FakeDPadGamepad();
        fake.Hold(Buttons.DPadRight);
        var input = new GamepadDPadInput2D(fake);

        input.X.ShouldBe(1f);
    }

    [Fact]
    public void Y_DPadUpHeld_ReturnsPositiveOne()
    {
        // Y+ is up by convention (matches world-space coordinates) — the most likely source of
        // a sign-flip bug if this adapter were ever rewritten.
        var fake = new FakeDPadGamepad();
        fake.Hold(Buttons.DPadUp);
        var input = new GamepadDPadInput2D(fake);

        input.Y.ShouldBe(1f);
    }
}

// --- InputManager.GetGamepad bounds check ---

public class InputManagerGetGamepadTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void GetGamepad_OutOfRange_Throws(int index)
    {
        var manager = new InputManager();

        Should.Throw<ArgumentOutOfRangeException>(() => manager.GetGamepad(index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GetGamepad_ValidIndex_ReturnsGamepad(int index)
    {
        var manager = new InputManager();

        var result = manager.GetGamepad(index);

        result.ShouldNotBeNull();
    }
}
