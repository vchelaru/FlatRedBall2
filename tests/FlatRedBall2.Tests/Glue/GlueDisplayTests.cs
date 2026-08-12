using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Entities;
using FlatRedBall2.Glue;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers translating Glue's project-level display block onto FRB2's DisplaySettings, and building
// the camera controller. Both sides are small; the traps are all in defaults and obsolete names.
public class GlueDisplayTests
{
    private static string Gluj(string project) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, project + ".gluj");

    [Fact]
    public void Deserialize_DisplaySettingsOmittingScale_DefaultsToOneHundredPercent()
    {
        // Glue omits defaults, so Scale is absent whenever it is 100 — and a window sized at
        // resolution × 0/100 has no pixels in it.
        string json = @"{ ""ResolutionWidth"": 320, ""ResolutionHeight"": 240 }";

        var settings = JsonSerializer.Deserialize(json, GlueJsonContext.Default.DisplaySettings)!;

        settings.Scale.ShouldBe(100);
        settings.ScaleGum.ShouldBe(100);
        settings.GenerateDisplayCode.ShouldBeTrue();
        // Height, not Width — reading it as default(int) picks the wrong axis.
        settings.DominantInternalCoordinates.ShouldBe(1);
    }

    [Fact]
    public void Apply_DoorsDemo_MapsResolutionScaleAndAspectLock()
    {
        // 256×224 at 300% with an 8:7 lock is what the project actually says.
        var project = GlueProject.Load(Gluj("DoorsDemo"));
        var target = new DisplaySettings();

        GlueDisplayMapper.Apply(project.Result.Project.DisplaySettings!, target, new());

        target.ResolutionWidth.ShouldBe(256);
        target.ResolutionHeight.ShouldBe(224);
        target.PreferredWindowWidth.ShouldBe(768);
        target.PreferredWindowHeight.ShouldBe(672);
        target.AspectPolicy.ShouldBe(AspectPolicy.Locked);
        target.FixedAspectRatio!.Value.ShouldBe(8f / 7f, tolerance: 0.0001);
        target.DominantAxis.ShouldBe(DominantAxis.Height);
        target.WindowMode.ShouldBe(WindowMode.Windowed);
    }

    // A hand-built block, so each field can be varied on its own — the fixture projects only cover
    // the combinations they happen to use, and an unmapped field looks identical to a mapped one
    // whose project happens to want the default.
    private static FlatRedBall2.Glue.Model.DisplaySettings Block() => new()
    {
        GenerateDisplayCode = true,
        ResolutionWidth = 320,
        ResolutionHeight = 240,
        Scale = 100,
        ScaleGum = 100,
        TextureFilter = 1,
        Is2D = true,
        DominantInternalCoordinates = 1,
    };

    private static DisplaySettings Applied(
        Action<FlatRedBall2.Glue.Model.DisplaySettings> author,
        List<GlueLoadDiagnostic>? diagnostics = null)
    {
        var source = Block();
        author(source);
        var target = new DisplaySettings();
        GlueDisplayMapper.Apply(source, target, diagnostics ?? new());
        return target;
    }

    // Both sides declare StretchVisibleArea then IncreaseVisibleArea, so the mapper casts the
    // ordinal. If either side ever reorders, a project silently gets the other resize behaviour.
    [Theory]
    [InlineData(0, ResizeMode.StretchVisibleArea)]
    [InlineData(1, ResizeMode.IncreaseVisibleArea)]
    public void Apply_ResizeBehavior_MapsOntoResizeMode(int authored, ResizeMode expected) =>
        Applied(s => s.ResizeBehavior = authored).ResizeMode.ShouldBe(expected);

    [Theory]
    [InlineData(0, DominantAxis.Width)]
    [InlineData(1, DominantAxis.Height)]
    public void Apply_DominantInternalCoordinates_MapsOntoDominantAxis(int authored, DominantAxis expected) =>
        Applied(s => s.DominantInternalCoordinates = authored).DominantAxis.ShouldBe(expected);

    [Theory]
    [InlineData(true, WindowMode.FullscreenBorderless)]
    [InlineData(false, WindowMode.Windowed)]
    public void Apply_RunInFullScreen_MapsOntoWindowMode(bool authored, WindowMode expected) =>
        Applied(s => s.RunInFullScreen = authored).WindowMode.ShouldBe(expected);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Apply_AllowWindowResizing_MapsOntoAllowUserResizing(bool authored) =>
        Applied(s => s.AllowWindowResizing = authored).AllowUserResizing.ShouldBe(authored);

    [Fact]
    public void Apply_Scale_SizesTheWindowFromResolution()
    {
        var target = Applied(s => s.Scale = 300);

        target.PreferredWindowWidth.ShouldBe(960);
        target.PreferredWindowHeight.ShouldBe(720);
        // The world is still the authored resolution — scale is a window multiplier, not a resize.
        target.ResolutionWidth.ShouldBe(320);
    }

    [Fact]
    public void Apply_FixedAspectRatio_LocksToTheAuthoredRatio()
    {
        var target = Applied(s =>
        {
            s.AspectRatioBehavior = 1;
            s.AspectRatioWidth = 16;
            s.AspectRatioHeight = 9;
        });

        target.AspectPolicy.ShouldBe(AspectPolicy.Locked);
        target.FixedAspectRatio!.Value.ShouldBe(16f / 9f, tolerance: 0.0001);
    }

    // FRB2 has no band between locked and free, so the range collapses to its first ratio. Silent
    // collapse would letterbox differently than the author asked without saying so.
    [Fact]
    public void Apply_RangedAspectRatio_LocksAndSaysSo()
    {
        var diagnostics = new List<GlueLoadDiagnostic>();

        var target = Applied(s =>
        {
            s.AspectRatioBehavior = 2;
            s.AspectRatioWidth = 16;
            s.AspectRatioHeight = 9;
        }, diagnostics);

        target.AspectPolicy.ShouldBe(AspectPolicy.Locked);
        diagnostics.ShouldContain(d => d.Message.Contains("ranged aspect ratio"));
    }

    [Theory]
    [InlineData(2, "Texture filter")]
    public void Apply_ATextureFilterFrb2CannotHonor_IsReported(int filter, string expected)
    {
        var diagnostics = new List<GlueLoadDiagnostic>();

        Applied(s => s.TextureFilter = filter, diagnostics);

        diagnostics.ShouldContain(d => d.Message.Contains(expected));
    }

    [Fact]
    public void Apply_AGumScaleFrb2CannotHonor_IsReported()
    {
        var diagnostics = new List<GlueLoadDiagnostic>();

        Applied(s => s.ScaleGum = 200, diagnostics);

        diagnostics.ShouldContain(d => d.Message.Contains("Gum scale"));
    }

    [Fact]
    public void Apply_ANonTwoDProject_IsReported()
    {
        var diagnostics = new List<GlueLoadDiagnostic>();

        Applied(s => s.Is2D = false, diagnostics);

        diagnostics.ShouldContain(d => d.Message.Contains("not authored as 2D"));
    }

    // The two layers joined up: an aspect authored in Glue has to reach the viewport maths, or the
    // mapping is correct and the game still fills the window. 16:9 locked inside a 4:3 window is
    // the case that produces bars.
    [Fact]
    public void Apply_ALockedAspectAuthoredInGlue_LetterboxesTheRunningCamera()
    {
        var engine = new FlatRedBallService();
        var source = Block();
        source.AspectRatioBehavior = 1;
        source.AspectRatioWidth = 16;
        source.AspectRatioHeight = 9;
        GlueDisplayMapper.Apply(source, engine.DisplaySettings, new());
        var camera = new Camera();

        engine.ApplyClientSizeChange(800, 600, allowUserResizing: true, camera);

        // 16:9 inside 4:3 fills the width and bars the top and bottom: 800 / (16/9) = 450, so
        // (600 - 450) / 2 = 75 per bar.
        camera.Viewport.Width.ShouldBe(800);
        camera.Viewport.Height.ShouldBe(450);
        camera.Viewport.Y.ShouldBe(75);
    }

    [Fact]
    public void Apply_BeefballWhichCarriesAStaleRatio_DoesNotLockAspect()
    {
        // Beefball carries AspectRatioWidth/Height of 16:9 with AspectRatioBehavior absent, i.e.
        // NoAspectRatio. Reading the ratio unconditionally would letterbox a game that should fill.
        var project = GlueProject.Load(Gluj("Beefball"));
        var target = new DisplaySettings();

        GlueDisplayMapper.Apply(project.Result.Project.DisplaySettings!, target, new());

        target.AspectPolicy.ShouldBe(AspectPolicy.Free);
        target.FixedAspectRatio.ShouldBeNull();
    }

    [Fact]
    public void Apply_GenerateDisplayCodeFalse_AppliesNothing()
    {
        // The author has opted out and hand-writes their own camera setup.
        var source = JsonSerializer.Deserialize(
            @"{ ""GenerateDisplayCode"": false, ""ResolutionWidth"": 640, ""ResolutionHeight"": 480 }",
            GlueJsonContext.Default.DisplaySettings)!;
        var target = new DisplaySettings { ResolutionWidth = 1280, ResolutionHeight = 720 };

        GlueDisplayMapper.Apply(source, target, new());

        target.ResolutionWidth.ShouldBe(1280);
    }

    [Fact]
    public void BuildObjects_CameraControllingEntity_IsBuiltAndTargetsTheNamedList()
    {
        // Glue can author only nine of this entity's properties, and two use FRB1's obsolete names.
        var project = GlueProject.Load(Gluj("DoorsDemo"));
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        screen.BuildObjects();

        var camera = screen.Objects["CameraControllingEntityInstance"]
            .ShouldBeOfType<CameraControllingEntity>();

        camera.Targets.ShouldNotBeEmpty();
    }

    [Fact]
    public void BuildObjects_CameraControllingEntity_MapsLerpSmoothOntoApproachStyle()
    {
        // LerpSmooth and LerpCoefficient are the *only* names Glue writes; FRB1 kept them because
        // renaming would break existing projects. TargetApproachStyle never appears on disk.
        var save = JsonSerializer.Deserialize(@"{
            ""Name"": ""Screens\\Test"",
            ""NamedObjects"": [ {
                ""InstanceName"": ""Cam"",
                ""SourceClassType"": ""FlatRedBall.Entities.CameraControllingEntity"",
                ""InstructionSaves"": [
                    { ""Type"": ""bool"", ""Member"": ""LerpSmooth"", ""Value"": false },
                    { ""Type"": ""float"", ""Member"": ""LerpCoefficient"", ""Value"": 9.0 } ]
            } ]
        }", GlueJsonContext.Default.ScreenSave)!;

        var screen = new GlueScreen { Save = save };
        screen.BuildObjects();

        var camera = (CameraControllingEntity)screen.Objects["Cam"];

        camera.TargetApproachStyle.ShouldBe(TargetApproachStyle.Immediate);
        camera.TargetApproachCoefficient.ShouldBe(9f);
    }
}
