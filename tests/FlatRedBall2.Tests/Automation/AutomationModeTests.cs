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
    public void TryAdvanceFrame_BeforeAnyStep_ReturnsFalse()
    {
        var mode = MakeMode();

        mode.TryAdvanceFrame(0).ShouldBeFalse();
    }

    [Fact]
    public void ProcessLine_StepCommand_TryAdvanceFrameReturnsTrue()
    {
        var mode = MakeMode();

        mode.ProcessLine("{\"cmd\":\"step\"}");

        mode.TryAdvanceFrame(0).ShouldBeTrue();
    }

    [Fact]
    public void ProcessLine_StepCommandCount3_TryAdvanceFrameReturnsTrueThenFalse()
    {
        var mode = MakeMode();

        mode.ProcessLine("{\"cmd\":\"step\",\"count\":3}");

        mode.TryAdvanceFrame(0).ShouldBeTrue();
        mode.TryAdvanceFrame(1).ShouldBeTrue();
        mode.TryAdvanceFrame(2).ShouldBeTrue();
        mode.TryAdvanceFrame(3).ShouldBeFalse();
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
        mode.TryAdvanceFrame(1);

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
        mode.TryAdvanceFrame(1);

        var json = output.ToString().Trim();
        json.ShouldContain("\"ok\":true");
        json.ShouldContain("42");
    }
}

// --- AutomationMode command ordering ---

public class AutomationModeOrderingTests
{
    [Fact]
    public void QueryStepQuery_QueriesProcessInOrderAcrossFrame()
    {
        // A recorded NDJSON file should be reproducible: commands must be processed in the
        // order they were sent. A query that follows a step must observe the post-step frame.
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"screen\"}");
        mode.ProcessLine("{\"cmd\":\"step\"}");
        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"screen\"}");

        long frame = 0;
        // Drive frames until no step is pending.
        while (mode.TryAdvanceFrame(frame)) frame++;

        var lines = output.ToString().Trim().Split('\n');
        lines.Length.ShouldBe(2);
        lines[0].ShouldContain("\"frame\":0");
        lines[1].ShouldContain("\"frame\":1");
    }

    [Fact]
    public void StepCount_QueryAfterAllFramesObservesFinalFrame()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"step\",\"count\":5}");
        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"screen\"}");

        long frame = 0;
        while (mode.TryAdvanceFrame(frame)) frame++;

        var lines = output.ToString().Trim().Split('\n');
        lines.Length.ShouldBe(1);
        lines[0].ShouldContain("\"frame\":5");
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
        mode.TryAdvanceFrame(1);

        captured.ShouldBe(100.5, tolerance: 0.001);
    }
}

// --- Reflection-based entity query/set (no per-property registration required) ---

public class AutomationModeReflectionTests
{
    private class AutoTestEntity : Entity
    {
        public int Health { get; set; } = 5;
    }
    private class AutoTestScreen : Screen { }

    private static (FlatRedBallService engine, AutomationMode mode, StringWriter output) MakeWithFactory(out Factory<AutoTestEntity> factory)
    {
        var engine = new FlatRedBallService();
        var screen = new AutoTestScreen { Engine = engine };
        factory = new Factory<AutoTestEntity>(screen);
        var output = new StringWriter();
        var mode = new AutomationMode(engine, output);
        return (engine, mode, output);
    }

    [Fact]
    public void QueryByEntityTypeName_NoProviderRegistered_ReturnsInstanceSnapshots()
    {
        var (_, mode, output) = MakeWithFactory(out var factory);
        var e = factory.Create();
        e.X = 10f; e.Y = 20f; e.Health = 7;

        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"AutoTestEntity\"}");
        mode.TryAdvanceFrame(1);

        var json = output.ToString().Trim();
        json.ShouldContain("\"ok\":true");
        json.ShouldContain("\"X\":10");
        json.ShouldContain("\"Y\":20");
        json.ShouldContain("\"Health\":7");
    }

    [Fact]
    public void SetByEntityTypeName_NoSetterRegistered_AssignsViaReflection()
    {
        var (_, mode, output) = MakeWithFactory(out var factory);
        var e = factory.Create();

        mode.ProcessLine("{\"cmd\":\"set\",\"entity\":\"AutoTestEntity\",\"prop\":\"X\",\"value\":42.5}");
        mode.TryAdvanceFrame(1);

        e.X.ShouldBe(42.5f, tolerance: 0.001f);
        output.ToString().Trim().ShouldContain("\"ok\":true");
    }

    [Fact]
    public void SetByEntityTypeName_IntProperty_ConvertsFromDouble()
    {
        var (_, mode, output) = MakeWithFactory(out var factory);
        var e = factory.Create();

        mode.ProcessLine("{\"cmd\":\"set\",\"entity\":\"AutoTestEntity\",\"prop\":\"Health\",\"value\":3}");
        mode.TryAdvanceFrame(1);

        e.Health.ShouldBe(3);
    }

    [Fact]
    public void QueryEntities_ListsAllFactoriesAndInstances()
    {
        var (_, mode, output) = MakeWithFactory(out var factory);
        factory.Create();
        factory.Create();

        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"entities\"}");
        mode.TryAdvanceFrame(1);

        var json = output.ToString().Trim();
        json.ShouldContain("\"AutoTestEntity\"");
        // Two instances should appear in the array
        json.ShouldContain("\"Health\":5");
    }

    [Fact]
    public void RegisteredProvider_OverridesReflection_WhenNameCollides()
    {
        var (_, mode, output) = MakeWithFactory(out var factory);
        factory.Create();
        mode.RegisterStateProvider("AutoTestEntity", () => new { custom = "view" });

        mode.ProcessLine("{\"cmd\":\"query\",\"target\":\"AutoTestEntity\"}");
        mode.TryAdvanceFrame(1);

        output.ToString().Trim().ShouldContain("\"custom\":\"view\"");
    }
}
