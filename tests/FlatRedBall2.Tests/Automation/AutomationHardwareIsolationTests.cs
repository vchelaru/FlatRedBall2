using System;
using System.Collections.Generic;
using System.IO;
using FlatRedBall2.Automation;
using FlatRedBall2.Input;
using FlatRedBall2.Tests.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

using FrbKeyboard = FlatRedBall2.Input.Keyboard;

namespace FlatRedBall2.Tests.Automation;

// While automation is active, injected state is the only input. A real device must never drive the
// game: on KNI SDL2 Mouse.GetState() reports global button state, so an unfocused --frb-auto window
// would otherwise react to clicks the user makes in other apps.
public class AutomationHardwareIsolationTests : IDisposable
{
    private readonly List<FlatRedBallService> _engines = new();

    public void Dispose()
    {
        foreach (var engine in _engines)
            engine.Shutdown();
    }

    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    private static MouseState PressedMouse(int x = 0, int y = 0) =>
        new MouseState(x, y, 0, ButtonState.Pressed, ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static MouseState ReleasedMouse() =>
        new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released,
            ButtonState.Released, ButtonState.Released, ButtonState.Released);

    [Fact]
    public void CursorUpdate_InjectedOnlyAndNothingInjected_IgnoresHardwarePress()
    {
        var cursor = new Cursor();
        cursor.UseInjectedInputOnly();

        cursor.Update(PressedMouse(), Sec(0));

        cursor.PrimaryDown.ShouldBeFalse();
        cursor.PrimaryPressed.ShouldBeFalse();
    }

    [Fact]
    public void CursorUpdate_InjectedOnly_UsesInjectionOverHardware()
    {
        var cursor = new Cursor();
        cursor.UseInjectedInputOnly();

        cursor.Inject(120, 80, primary: true, secondary: false);
        cursor.Update(ReleasedMouse(), Sec(0));

        cursor.PrimaryDown.ShouldBeTrue();
        cursor.ScreenPosition.X.ShouldBe(120f);
    }

    [Fact]
    public void KeyboardUpdate_InjectedOnlyAndNothingInjected_IgnoresHardwareKey()
    {
        var keyboard = new FrbKeyboard();
        keyboard.UseInjectedInputOnly();

        keyboard.Update(new KeyboardState(Keys.Space));

        keyboard.IsKeyDown(Keys.Space).ShouldBeFalse();
    }

    [Fact]
    public void KeyboardUpdate_NotInjectedOnly_ReadsHardwareKey()
    {
        var keyboard = new FrbKeyboard();

        keyboard.Update(new KeyboardState(Keys.Space));

        keyboard.IsKeyDown(Keys.Space).ShouldBeTrue();
    }

    [Fact]
    public void GamepadUpdate_InjectedOnlyAndNothingInjected_IgnoresHardwarePad()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { Connected = true, Buttons = Buttons.A });
        gamepad.UseInjectedInputOnly();

        gamepad.Update();

        gamepad.IsButtonDown(Buttons.A).ShouldBeFalse();
        // A pad nobody injected into is not plugged in as far as the game can tell.
        gamepad.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void GamepadUpdate_NotInjectedOnly_ReadsHardwareButton()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { Connected = true, Buttons = Buttons.A });

        gamepad.Update();

        gamepad.IsButtonDown(Buttons.A).ShouldBeTrue();
    }

    [Fact]
    public void GumCursor_InjectedOnlyAndHardwarePressed_SeesNoPush()
    {
        var frb = new Cursor();
        frb.UseInjectedInputOnly();
        var gum = new AutomationGumCursor(frb);

        frb.Update(ReleasedMouse(), Sec(0));
        gum.Activity(0);
        frb.Update(PressedMouse(250, 175), Sec(0.016));
        gum.Activity(0.016);

        gum.PrimaryPush.ShouldBeFalse();
        gum.PrimaryDown.ShouldBeFalse();
    }

    [Fact]
    public void StartAutomationMode_HardwarePressOnMouseAndKeyboard_DoesNotReachEngineInput()
    {
        var engine = new FlatRedBallService();
        _engines.Add(engine);
        engine.StartAutomationMode(input: new StringReader(string.Empty));

        engine.Input.InternalCursor.Update(PressedMouse(), Sec(0));
        var keyboard = (FrbKeyboard)engine.Input.Keyboard;
        keyboard.Update(new KeyboardState(Keys.Space));

        engine.Input.Cursor.PrimaryDown.ShouldBeFalse();
        engine.Input.Keyboard.IsKeyDown(Keys.Space).ShouldBeFalse();
    }
}
