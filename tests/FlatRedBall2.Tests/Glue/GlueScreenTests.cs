using System;
using System.Collections.Generic;
using System.IO;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Every loaded screen shares one CLR type; what distinguishes them is their data. These cover the
// resolution step that picks which ScreenSave a GlueScreen is built from.
public class GlueScreenTests
{
    private static GlueLoadOptions InMemory(Dictionary<string, string> files) => new()
    {
        ResolveFilePath = p => files.ContainsKey(p) ? p : null,
        ReadAllText = p => files[p],
    };

    [Fact]
    public void GlueScreen_WithSave_ExposesItsGlueName()
    {
        var screen = new GlueScreen { Save = new ScreenSave { Name = @"Screens\Level1" } };

        screen.GlueName.ShouldBe(@"Screens\Level1");
        screen.ToString().ShouldBe(@"Screens\Level1");
    }

    [Fact]
    public void GlueScreen_WithoutSave_DoesNotThrowOnName()
    {
        // Save is assigned by the configure callback, so a screen briefly exists without one.
        var screen = new GlueScreen();

        screen.GlueName.ShouldBeNull();
        screen.ToString().ShouldBe(nameof(GlueScreen));
    }

    [Fact]
    public void Load_DoorsDemo_ResolvesStartUpScreenToTheDerivedLevel()
    {
        var glujPath = Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj");

        var result = GlueProjectLoader.Load(glujPath);

        result.StartUpScreen.ShouldNotBeNull();
        result.StartUpScreen.Name.ShouldBe(@"Screens\Level1");
        result.StartUpScreen.BaseScreen.ShouldBe(@"Screens\GameScreen");
        // The start-up screen is derived, so it arrives already merged with its base — nine objects
        // rather than the four its own file declares.
        result.StartUpScreen.NamedObjects.Count.ShouldBe(9);
    }

    [Fact]
    public void Load_NoStartUpScreenNamed_ResolvesToNullWithoutError()
    {
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = @"{ ""FileVersion"": 68 }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.StartUpScreen.ShouldBeNull();
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Load_StartUpScreenNamesAMissingScreen_ReportsErrorRatherThanNull()
    {
        // Silently handing back null here would surface as a NullReferenceException at boot, far
        // from the actual cause.
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = @"{ ""FileVersion"": 68, ""StartUpScreen"": ""Screens\\Gone"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.StartUpScreen.ShouldBeNull();
        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Error && d.Message.Contains(@"Screens\Gone"));
    }
}
