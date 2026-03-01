using System;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
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

// --- KeyboardPressableInput ---

public class KeyboardPressableInputTests
{
    [Fact]
    public void WasJustReleased_DelegatesToWasKeyJustReleased()
    {
        var fake = new FakeKeyboard { KeyReleased = true };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        Assert.True(input.WasJustReleased);
    }

    [Fact]
    public void WasJustReleased_ReturnsFalse_WhenNotReleased()
    {
        var fake = new FakeKeyboard { KeyReleased = false };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        Assert.False(input.WasJustReleased);
    }

    [Fact]
    public void WasJustPressed_DelegatesToWasKeyPressed()
    {
        var fake = new FakeKeyboard { KeyPressed = true };
        var input = new KeyboardPressableInput(fake, Keys.Space);

        Assert.True(input.WasJustPressed);
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

        Assert.True(input.WasJustPressed);
    }

    [Fact]
    public void WasJustReleased_DelegatesToWasButtonJustReleased()
    {
        var fake = new FakeGamepad { ButtonJustReleased = true };
        var input = new GamepadPressableInput(fake, Buttons.A);

        Assert.True(input.WasJustReleased);
    }

    [Fact]
    public void WasJustPressed_ReturnsFalse_WhenNotPressed()
    {
        var fake = new FakeGamepad { ButtonJustPressed = false };
        var input = new GamepadPressableInput(fake, Buttons.A);

        Assert.False(input.WasJustPressed);
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

        Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetGamepad(index));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GetGamepad_ValidIndex_ReturnsGamepad(int index)
    {
        var manager = new InputManager();

        var result = manager.GetGamepad(index);

        Assert.NotNull(result);
    }
}
