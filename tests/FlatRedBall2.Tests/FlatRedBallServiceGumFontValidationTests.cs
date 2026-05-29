using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FlatRedBallServiceGumFontValidationTests
{
    [Fact]
    public void IsAbsoluteContentPath_WindowsDriveAndRelativePaths_ReturnsExpectedValues()
    {
        FlatRedBallService.IsAbsoluteContentPath("C:/fonts/NotoSans-Regular.ttf").ShouldBeTrue();
        FlatRedBallService.IsAbsoluteContentPath("Fonts/Inter-Regular.ttf").ShouldBeFalse();
    }

    [Fact]
    public void ResolveGumFontValidationPaths_RelativeAndAbsolutePaths_UsesGumProjectDirectoryForRelative()
    {
        var settings = new EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx",
            GumFontFilesToValidate = new[]
            {
                "Fonts/Inter-Regular.ttf",
                @"C:\fonts\NotoSans-Regular.ttf"
            }
        };

        var paths = FlatRedBallService.ResolveGumFontValidationPaths(settings);

        paths.Count.ShouldBe(2);
        paths[0].ShouldBe("GumProject/Fonts/Inter-Regular.ttf");
        paths[1].ShouldBe("C:/fonts/NotoSans-Regular.ttf");
    }

    [Fact]
    public void ValidateConfiguredGumFontFiles_MissingAndThrowBehavior_ThrowsInvalidOperationException()
    {
        var settings = new EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx",
            GumFontFilesToValidate = new[] { "Fonts/Inter-Regular.ttf" },
            MissingGumFontFileBehavior = MissingGumFontFileBehavior.Throw
        };

        var warnings = new List<string>();

        Should.Throw<InvalidOperationException>(() =>
            FlatRedBallService.ValidateConfiguredGumFontFiles(settings, _ => false, warnings.Add));
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateConfiguredGumFontFiles_MissingAndWarnBehavior_ReportsWarningWithoutThrow()
    {
        var settings = new EngineInitSettings
        {
            GumProjectFile = "GumProject/GumProject.gumx",
            GumFontFilesToValidate = new[] { "Fonts/Inter-Regular.ttf" },
            MissingGumFontFileBehavior = MissingGumFontFileBehavior.Warn
        };

        var warnings = new List<string>();

        FlatRedBallService.ValidateConfiguredGumFontFiles(settings, _ => false, warnings.Add);

        warnings.Count.ShouldBe(1);
        warnings[0].ShouldContain("GumProject/Fonts/Inter-Regular.ttf");
    }
}
