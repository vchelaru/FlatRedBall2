using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

using FrbKeyboard = FlatRedBall2.Input.Keyboard;

namespace FlatRedBall2.Tests.Input;

public class GamepadAnyButtonTests
{
    private static Gamepad NewPad() => new Gamepad(0, new FakeGamepadPlatform());

    [Fact]
    public void GetButtonJustPressed_ButtonWentDownThisFrame_ReturnsThatButton()
    {
        var pad = NewPad();
        pad.Update();
        pad.InjectButton(Buttons.X, down: true);
        pad.Update();

        pad.GetButtonJustPressed().ShouldBe(Buttons.X);
        pad.WasAnyButtonJustPressed().ShouldBeTrue();
    }

    [Fact]
    public void GetButtonJustPressed_ButtonStillHeld_ReturnsNull()
    {
        var pad = NewPad();
        pad.InjectButton(Buttons.X, down: true);
        pad.Update();
        pad.Update();

        pad.GetButtonJustPressed().ShouldBeNull();
        pad.WasAnyButtonJustPressed().ShouldBeFalse();
    }

    [Fact]
    public void GetButtonJustPressed_NothingPressed_ReturnsNull()
    {
        var pad = NewPad();
        pad.Update();

        pad.GetButtonJustPressed().ShouldBeNull();
    }

    [Theory]
    [InlineData(Buttons.DPadUp)]
    [InlineData(Buttons.LeftThumbstickLeft)]
    [InlineData(Buttons.RightThumbstickUp)]
    public void GetButtonJustPressed_DirectionalWithIgnoreDirectionals_ReturnsNull(Buttons directional)
    {
        var pad = NewPad();
        pad.Update();
        pad.InjectButton(directional, down: true);
        pad.Update();

        pad.GetButtonJustPressed().ShouldBe(directional);
        pad.GetButtonJustPressed(ignoreDirectionals: true).ShouldBeNull();
    }

    [Theory]
    [InlineData(Buttons.LeftTrigger)]
    [InlineData(Buttons.RightTrigger)]
    [InlineData(Buttons.LeftThumbstickRight)]
    public void GetButtonJustPressed_AnalogWithIgnoreAnalogs_ReturnsNull(Buttons analog)
    {
        var pad = NewPad();
        pad.Update();
        pad.InjectButton(analog, down: true);
        pad.Update();

        pad.GetButtonJustPressed().ShouldBe(analog);
        pad.GetButtonJustPressed(ignoreAnalogs: true).ShouldBeNull();
    }

    [Fact]
    public void GetButtonJustPressed_IgnoredButtonPlusFaceButton_ReturnsFaceButton()
    {
        var pad = NewPad();
        pad.Update();
        pad.InjectButton(Buttons.DPadUp, down: true);
        pad.InjectButton(Buttons.B, down: true);
        pad.Update();

        pad.GetButtonJustPressed(ignoreDirectionals: true, ignoreAnalogs: true).ShouldBe(Buttons.B);
    }
}

public class KeyboardAnyKeyTests
{
    [Fact]
    public void GetKeyPressed_KeyWentDownThisFrame_ReturnsThatKey()
    {
        var keyboard = new FrbKeyboard();
        keyboard.Update();
        keyboard.InjectKey(Keys.Q, down: true);
        keyboard.Update();

        keyboard.GetKeyPressed().ShouldBe(Keys.Q);
        keyboard.WasAnyKeyPressed().ShouldBeTrue();
    }

    [Fact]
    public void GetKeyPressed_KeyStillHeld_ReturnsNull()
    {
        var keyboard = new FrbKeyboard();
        keyboard.InjectKey(Keys.Q, down: true);
        keyboard.Update();
        keyboard.Update();

        keyboard.GetKeyPressed().ShouldBeNull();
        keyboard.WasAnyKeyPressed().ShouldBeFalse();
    }
}
