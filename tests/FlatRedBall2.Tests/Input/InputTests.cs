using System;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

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
