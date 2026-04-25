using System;
using System.IO;
using System.Text.Json;
using FlatRedBall2.Automation;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

using FrbKeyboard = FlatRedBall2.Input.Keyboard;

namespace FlatRedBall2.Tests.Automation;

// --- Keyboard injection ---

public class KeyboardInjectionTests
{
    [Fact]
    public void InjectKey_SpaceDown_IsKeyDownReturnsTrue()
    {
        var keyboard = new FrbKeyboard();

        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();

        keyboard.IsKeyDown(Keys.Space).ShouldBeTrue();
    }

    [Fact]
    public void InjectKey_SpaceUpAfterDown_IsKeyDownReturnsFalse()
    {
        var keyboard = new FrbKeyboard();

        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();
        keyboard.InjectKey(Keys.Space, down: false);
        keyboard.Update();

        keyboard.IsKeyDown(Keys.Space).ShouldBeFalse();
    }

    [Fact]
    public void InjectKey_SpaceDown_WasKeyPressedTrueFirstFrameOnly()
    {
        var keyboard = new FrbKeyboard();

        keyboard.InjectKey(Keys.Space, down: true);
        keyboard.Update();
        bool firstFrame = keyboard.WasKeyPressed(Keys.Space);

        keyboard.Update();
        bool secondFrame = keyboard.WasKeyPressed(Keys.Space);

        firstFrame.ShouldBeTrue();
        secondFrame.ShouldBeFalse();
    }
}

// --- Gamepad injection ---

public class GamepadInjectionTests
{
    [Fact]
    public void InjectAxis_LeftStickX_GetAxisReturnsValue()
    {
        var gamepad = new Gamepad(0);

        gamepad.InjectAxis(GamepadAxis.LeftStickX, 0.75f);
        gamepad.Update();

        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0.75f, tolerance: 0.001f);
    }

    [Fact]
    public void InjectButton_ADown_IsButtonDownReturnsTrue()
    {
        var gamepad = new Gamepad(0);

        gamepad.InjectButton(Buttons.A, down: true);
        gamepad.Update();

        gamepad.IsButtonDown(Buttons.A).ShouldBeTrue();
    }
}

// --- AutomationMode step gating ---

public class AutomationModeStepTests
{
    private static AutomationMode MakeMode() => new AutomationMode(new FlatRedBallService(), new StringWriter());

    [Fact]
    public void ConsumeStep_BeforeAnyStep_ReturnsFalse()
    {
        var mode = MakeMode();

        mode.ConsumeStep().ShouldBeFalse();
    }

    [Fact]
    public void ProcessLine_StepCommand_ConsumeStepReturnsTrue()
    {
        var mode = MakeMode();

        mode.ProcessLine("{\"cmd\":\"step\"}");

        mode.ConsumeStep().ShouldBeTrue();
    }

    [Fact]
    public void ProcessLine_StepCommandCount3_ConsumeStepReturnsTrueThenFalse()
    {
        var mode = MakeMode();

        mode.ProcessLine("{\"cmd\":\"step\",\"count\":3}");

        mode.ConsumeStep().ShouldBeTrue();
        mode.ConsumeStep().ShouldBeTrue();
        mode.ConsumeStep().ShouldBeTrue();
        mode.ConsumeStep().ShouldBeFalse();
    }
}

// --- AutomationMode query ---

public class AutomationModeQueryTests
{
    [Fact]
    public void ProcessQueuedCommands_QueryScreen_WritesJsonResponse()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"screen\"}");
        mode.ProcessQueuedCommands(frame: 1);

        var json = output.ToString().Trim();
        json.ShouldContain("\"ok\":true");
        json.ShouldContain("\"screen\"");
    }

    [Fact]
    public void RegisterStateProvider_ThenQueryByName_ReturnsProviderData()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.RegisterStateProvider("score", () => new { value = 42 });
        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"score\"}");
        mode.ProcessQueuedCommands(frame: 1);

        var json = output.ToString().Trim();
        json.ShouldContain("\"ok\":true");
        json.ShouldContain("42");
    }
}

// --- AutomationMode set ---

public class AutomationModeSetTests
{
    [Fact]
    public void RegisterValueSetter_ThenSetCommand_CallsSetterWithValue()
    {
        var mode = new AutomationMode(new FlatRedBallService(), new StringWriter());
        double captured = 0;
        mode.RegisterValueSetter("Player", "X", v => captured = v);

        mode.ProcessLine("{\"cmd\":\"set\",\"entity\":\"Player\",\"prop\":\"X\",\"value\":100.5}");
        mode.ProcessQueuedCommands(frame: 1);

        captured.ShouldBe(100.5, tolerance: 0.001);
    }
}
