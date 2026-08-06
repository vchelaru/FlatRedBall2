using System;
using System.IO;
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
