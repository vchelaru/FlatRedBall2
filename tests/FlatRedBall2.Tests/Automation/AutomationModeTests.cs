using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FlatRedBall2.Automation;
using FlatRedBall2.Input;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
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

// --- Cursor injection ---

public class CursorInjectionTests
{
    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void Inject_ScreenPosition_AppearsAfterUpdate()
    {
        var cursor = new Cursor();

        cursor.Inject(120, 80, primary: false, secondary: false);
        cursor.Update(Sec(0));

        cursor.ScreenPosition.X.ShouldBe(120f);
        cursor.ScreenPosition.Y.ShouldBe(80f);
    }

    [Fact]
    public void Inject_PrimaryDown_PrimaryDownReturnsTrue()
    {
        var cursor = new Cursor();

        cursor.Inject(0, 0, primary: true, secondary: false);
        cursor.Update(Sec(0));

        cursor.PrimaryDown.ShouldBeTrue();
        cursor.PrimaryPressed.ShouldBeTrue();
    }

    [Fact]
    public void Inject_PressThenRelease_PrimaryClickFiresOnReleaseFrame()
    {
        var cursor = new Cursor();

        cursor.Inject(0, 0, primary: true, secondary: false);
        cursor.Update(Sec(0));
        cursor.Inject(0, 0, primary: false, secondary: false);
        cursor.Update(Sec(0.01));

        cursor.PrimaryClick.ShouldBeTrue();
        cursor.PrimaryDown.ShouldBeFalse();
    }

    [Fact]
    public void Inject_TwoPressesWithinThreshold_PrimaryDoublePressedFires()
    {
        var cursor = new Cursor();

        cursor.Inject(0, 0, primary: true, secondary: false);  cursor.Update(Sec(0.00));
        cursor.Inject(0, 0, primary: false, secondary: false); cursor.Update(Sec(0.05));
        cursor.Inject(0, 0, primary: true, secondary: false);  cursor.Update(Sec(0.10));

        cursor.PrimaryDoublePressed.ShouldBeTrue();
    }

    [Fact]
    public void Inject_Secondary_SecondaryDownReturnsTrue()
    {
        var cursor = new Cursor();

        cursor.Inject(0, 0, primary: false, secondary: true);
        cursor.Update(Sec(0));

        cursor.SecondaryDown.ShouldBeTrue();
    }
}

// --- AutomationMode cursor command (NDJSON) ---

public class AutomationModeCursorCommandTests
{
    private const int HostWidth = 1280;
    private const int HostHeight = 720;
    private const int OrthoHeight = 720;
    private static readonly Viewport Host = new Viewport(0, 0, HostWidth, HostHeight);

    private static Camera MakeCamera(float worldX, float worldY)
    {
        var cam = new Camera { X = worldX, Y = worldY, NormalizedViewport = new NormalizedRectangle(0f, 0f, 1f, 1f) };
        cam.ApplyToHostRect(Host, OrthoHeight);
        return cam;
    }

    [Fact]
    public void CursorCommand_ScreenSpace_UpdatesScreenPositionAndPrimary()
    {
        var engine = new FlatRedBallService();
        var mode = new AutomationMode(engine, new StringWriter());

        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":120,\"y\":80,\"primary\":true}");
        mode.TryAdvanceFrame(0);
        engine.Input.Update(TimeSpan.Zero);

        engine.Input.Cursor.ScreenPosition.X.ShouldBe(120f);
        engine.Input.Cursor.ScreenPosition.Y.ShouldBe(80f);
        engine.Input.Cursor.PrimaryDown.ShouldBeTrue();
    }

    [Fact]
    public void CursorCommand_WorldSpace_BackProjectsThroughActiveCamera()
    {
        var engine = new FlatRedBallService();
        var camera = MakeCamera(worldX: 0f, worldY: 0f);
        engine.Input.SetCameras(new List<Camera> { camera });
        var mode = new AutomationMode(engine, new StringWriter());

        // World (0,0) sits at viewport center → screen (640, 360).
        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":0,\"y\":0,\"space\":\"world\"}");
        mode.TryAdvanceFrame(0);
        engine.Input.Update(TimeSpan.Zero);

        engine.Input.Cursor.WorldPosition.X.ShouldBe(0f, tolerance: 1f);
        engine.Input.Cursor.WorldPosition.Y.ShouldBe(0f, tolerance: 1f);
        engine.Input.Cursor.ScreenPosition.X.ShouldBe(HostWidth / 2f, tolerance: 1f);
        engine.Input.Cursor.ScreenPosition.Y.ShouldBe(HostHeight / 2f, tolerance: 1f);
    }

    [Fact]
    public void CursorCommand_WorldSpace_OffCenter_RoundtripsThroughCameraTransform()
    {
        var engine = new FlatRedBallService();
        var camera = MakeCamera(worldX: 100f, worldY: 50f);
        engine.Input.SetCameras(new List<Camera> { camera });
        var mode = new AutomationMode(engine, new StringWriter());

        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":250,\"y\":-30,\"space\":\"world\"}");
        mode.TryAdvanceFrame(0);
        engine.Input.Update(TimeSpan.Zero);

        engine.Input.Cursor.WorldPosition.X.ShouldBe(250f, tolerance: 1f);
        engine.Input.Cursor.WorldPosition.Y.ShouldBe(-30f, tolerance: 1f);
    }

    [Fact]
    public void CursorCommand_UnknownSpace_WritesError()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"cursor\",\"x\":0,\"y\":0,\"space\":\"zoid\"}");
        mode.TryAdvanceFrame(0);

        output.ToString().ShouldContain("\"ok\":false");
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

// --- AutomationMode screenshot ---

public class AutomationModeScreenshotTests
{
    [Fact]
    public void ProcessLine_RecordNextScreenshotCommand_ArmsPendingRequestWithoutImmediateResponse()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"record_next_screenshot\",\"path\":\"out.png\"}");
        mode.TryAdvanceFrame(0);

        output.ToString().ShouldBeEmpty();
        mode.TryConsumePendingScreenshot(out var path).ShouldBeTrue();
        path.ShouldBe("out.png");
    }

    [Fact]
    public void ProcessLine_RecordNextScreenshotCommand_MissingPath_WritesErrorAndDoesNotArm()
    {
        var output = new StringWriter();
        var mode = new AutomationMode(new FlatRedBallService(), output);

        mode.ProcessLine("{\"cmd\":\"record_next_screenshot\"}");
        mode.TryAdvanceFrame(0);

        output.ToString().ShouldContain("\"ok\":false");
        mode.TryConsumePendingScreenshot(out _).ShouldBeFalse();
    }

    [Fact]
    public void TryConsumePendingScreenshot_CalledTwice_OnlyFiresOnce()
    {
        var mode = new AutomationMode(new FlatRedBallService(), new StringWriter());
        mode.ProcessLine("{\"cmd\":\"record_next_screenshot\",\"path\":\"out.png\"}");
        mode.TryAdvanceFrame(0);

        mode.TryConsumePendingScreenshot(out _);
        var second = mode.TryConsumePendingScreenshot(out _);

        second.ShouldBeFalse();
    }
}
