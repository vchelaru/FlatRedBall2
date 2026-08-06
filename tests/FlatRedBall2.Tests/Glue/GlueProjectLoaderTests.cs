using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers resolving a .gluj into its .glsj/.glej element files. Every read goes through an
// injectable seam so these tests never touch disk except where they explicitly load a fixture.
public class GlueProjectLoaderTests
{
    private const string MinimalGluj = @"{
        ""FileVersion"": 68,
        ""StartUpScreen"": ""Screens\\Level1"",
        ""ScreenReferences"": [ { ""Name"": ""Screens\\Level1"" } ]
    }";

    private static string FixtureDirectory(string project) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project);

    /// <summary>An in-memory filesystem keyed by exact path, for tests that never touch disk.</summary>
    private static GlueLoadOptions InMemory(Dictionary<string, string> files, bool caseSensitive = true)
    {
        return new GlueLoadOptions
        {
            ResolveFilePath = requested =>
            {
                if (files.ContainsKey(requested))
                    return requested;
                if (caseSensitive)
                    return null;
                return files.Keys.FirstOrDefault(
                    k => string.Equals(k, requested, StringComparison.OrdinalIgnoreCase));
            },
            ReadAllText = path => files[path],
        };
    }

    [Fact]
    public void Load_BelowLatestVersion_ReportsInfoNotError()
    {
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = @"{ ""FileVersion"": 42 }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Info && d.Message.Contains("42"));
    }

    [Fact]
    public void Load_CaseMismatchedReference_LoadsAndWarns()
    {
        // Glue authors these names on Windows. A project that works there must not silently lose a
        // screen on Linux — but the author should still hear about it.
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = MinimalGluj,
            [@"C:\proj\Screens/level1.glsj"] = @"{ ""Name"": ""Screens\\Level1"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files, caseSensitive: false));

        result.Project.Screens.Count.ShouldBe(1);
        result.Diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("case"));
    }

    [Fact]
    public void Load_DoorsDemoFixture_ResolvesAllElementsAndClearsReferences()
    {
        var result = GlueProjectLoader.Load(Path.Combine(FixtureDirectory("DoorsDemo"), "DoorsDemo.gluj"));

        result.Project.Screens.Count.ShouldBe(2);
        result.Project.Entities.Count.ShouldBe(2);
        result.Project.ScreenReferences.ShouldBeEmpty();
        result.Project.EntityReferences.ShouldBeEmpty();
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Load_ElementNameDisagreesWithFilePath_WarnsAndKeepsPathAuthoritative()
    {
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = MinimalGluj,
            [@"C:\proj\Screens/Level1.glsj"] = @"{ ""Name"": ""Screens\\SomethingElse"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.Project.Screens.Count.ShouldBe(1);
        result.Diagnostics.ShouldContain(d => d.Severity == GlueDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Load_MissingElementFile_WarnsAndKeepsLoadingTheRest()
    {
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = @"{
                ""FileVersion"": 68,
                ""ScreenReferences"": [ { ""Name"": ""Screens\\Gone"" }, { ""Name"": ""Screens\\Here"" } ]
            }",
            [@"C:\proj\Screens/Here.glsj"] = @"{ ""Name"": ""Screens\\Here"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.Project.Screens.Count.ShouldBe(1);
        result.Diagnostics.Count(d => d.Severity == GlueDiagnosticSeverity.Warning).ShouldBe(1);
    }

    [Fact]
    public void Load_ReadsOnlyThroughTheInjectedSeam()
    {
        var requested = new List<string>();
        var options = new GlueLoadOptions
        {
            ResolveFilePath = p => { requested.Add(p); return p; },
            ReadAllText = _ => @"{ ""FileVersion"": 68 }",
        };

        GlueProjectLoader.Load(@"Z:\does\not\exist\Test.gluj", options);

        // A path that cannot exist proves nothing fell through to System.IO.
        requested.ShouldContain(@"Z:\does\not\exist\Test.gluj");
    }

    [Fact]
    public void Load_ReferenceName_ResolvesBackslashesToPathSeparators()
    {
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = MinimalGluj,
            [@"C:\proj\Screens/Level1.glsj"] = @"{ ""Name"": ""Screens\\Level1"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.Project.Screens.Count.ShouldBe(1);
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Load_StartUpScreen_MatchesOnUnnormalizedName()
    {
        // Normalization is for building paths only. StartUpScreen and BaseScreen are identities and
        // must still compare against the original backslash form.
        var files = new Dictionary<string, string>
        {
            [@"C:\proj\Test.gluj"] = MinimalGluj,
            [@"C:\proj\Screens/Level1.glsj"] = @"{ ""Name"": ""Screens\\Level1"" }",
        };

        var result = GlueProjectLoader.Load(@"C:\proj\Test.gluj", InMemory(files));

        result.Project.StartUpScreen.ShouldBe(@"Screens\Level1");
        result.Project.Screens[0].Name.ShouldBe(@"Screens\Level1");
    }

    [Fact]
    public void Load_StrictWithUnreadableProject_Throws()
    {
        var files = new Dictionary<string, string>();
        var options = InMemory(files);
        options.Strict = true;

        Should.Throw<GlueLoadException>(() => GlueProjectLoader.Load(@"C:\proj\Missing.gluj", options));
    }

    [Fact]
    public void Load_UnreadableProjectFile_ReportsErrorWithoutThrowing()
    {
        var result = GlueProjectLoader.Load(@"C:\proj\Missing.gluj", InMemory(new Dictionary<string, string>()));

        result.HasErrors.ShouldBeTrue();
        result.Project.ShouldNotBeNull();
    }
}
