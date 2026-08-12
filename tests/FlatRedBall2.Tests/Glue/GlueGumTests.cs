using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers finding a Glue project's Gum project and turning its .gusx references into the element
// names Gum looks up. Resolution is pure data and runs anywhere; instantiating the visual needs a
// real device and lives in GlueGumInstantiationTests.
//
// The thing to know (G57): a Gum project only works when GumService itself loaded it. Assigning
// ObjectFinder.Self.GumProjectSave from a hand-rolled GumProjectSave.Load builds children and then
// throws deep in UnitConverter, which reads like bad data rather than bad setup.
public class GlueGumTests
{
    private static string FixturePath(string project, params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Glue", "Fixtures", project }
            .Concat(parts).ToArray());

    private static GlueLoadResult LoadDoorsDemo() =>
        GlueProjectLoader.Load(FixturePath("DoorsDemo", "DoorsDemo.gluj"));

    [Fact]
    public void Load_ProjectWithAGumProjectInGlobalFiles_FindsIt()
    {
        var result = LoadDoorsDemo();

        result.GumProjectFile.ShouldBe("GumProject/GumProject.gumx");
    }

    [Fact]
    public void Load_ProjectWithNoGumProject_LeavesGumProjectFileNull()
    {
        var project = new GlueProjectSave();

        GlueGumResolver.FindGumProjectFile(project).ShouldBeNull();
    }

    [Theory]
    // The gum project folder and the screens/ or components/ segment both come off; what remains is
    // the element name Gum knows the element by.
    [InlineData("GumProject/Screens/GameScreenGum.gusx", "GameScreenGum")]
    [InlineData("GumProject/Components/CardGum.gucx", "CardGum")]
    // A nested component keeps the folders below components/ — Gum's own names carry them
    // ("Controls/ButtonStandard"), so stripping them would break the lookup.
    [InlineData("GumProject/Components/Controls/ButtonStandard.gucx", "Controls/ButtonStandard")]
    // Backslashes appear in element files even though Name is documented as forward-slashed.
    [InlineData(@"GumProject\Screens\GameScreenGum.gusx", "GameScreenGum")]
    public void ElementNameFor_AGumFileReference_StripsProjectFolderAndCategory(
        string referencedFileName, string expected)
    {
        GlueGumResolver.ElementNameFor(referencedFileName, "GumProject/GumProject.gumx")
            .ShouldBe(expected);
    }

    [Fact]
    public void GumScreenNameFor_AScreenDeclaringOne_ResolvesFromItsReferencedFiles()
    {
        var result = LoadDoorsDemo();
        var gameScreen = result.Project.Screens.Single(s => s.Name == @"Screens\GameScreen");

        GlueGumResolver.GumElementNameFor(gameScreen, result.Project, result.GumProjectFile)
            .ShouldBe("GameScreenGum");
    }

    // G54 — three different RuntimeType values appear for .gusx and only one names a real class, so
    // the loader never uses it for lookup. A project whose RuntimeType names nothing that exists
    // still resolves.
    [Fact]
    public void GumScreenNameFor_ARuntimeTypeNamingANonexistentClass_StillResolves()
    {
        var screen = new ScreenSave
        {
            Name = @"Screens\GameScreen",
            ReferencedFiles =
            {
                new ReferencedFileSave
                {
                    Name = "GumProject/Screens/GameScreenGum.gusx",
                    RuntimeType = "DoesNotExist.GumRuntimes.NotARealTypeRuntime",
                },
            },
        };

        GlueGumResolver.GumElementNameFor(screen, new GlueProjectSave(), "GumProject/GumProject.gumx")
            .ShouldBe("GameScreenGum");
    }

    // G53 — Level1 declares no .gusx of its own and inherits GameScreenGum. Since Level1 is the
    // project's StartUpScreen, the inheriting case is the primary fixture's main path, not an edge.
    [Fact]
    public void GumScreenNameFor_ADerivedScreenDeclaringNoGumScreen_InheritsItsBases()
    {
        // The file on disk really does declare none — otherwise this test proves nothing.
        File.ReadAllText(FixturePath("DoorsDemo", "Screens", "Level1.glsj"))
            .ShouldNotContain(".gusx");

        var result = LoadDoorsDemo();
        var level1 = result.Project.Screens.Single(s => s.Name == @"Screens\Level1");

        GlueGumResolver.GumElementNameFor(level1, result.Project, result.GumProjectFile)
            .ShouldBe("GameScreenGum");
    }

    // The inherited .gusx arrives through Phase 6's ReferencedFiles merge, not through a Gum-specific
    // base walk. Pinned because it is why G53 needed no special handling — if flattening ever stops
    // merging referenced files, the test above would still pass via the resolver's own fallback and
    // the reason for that fallback would be lost.
    [Fact]
    public void Load_ADerivedScreen_InheritsItsBasesGumFileByFlattening()
    {
        var result = LoadDoorsDemo();
        var level1 = result.Project.Screens.Single(s => s.Name == @"Screens\Level1");

        level1.ReferencedFiles.ShouldContain(
            f => f.Name!.EndsWith(".gusx", StringComparison.OrdinalIgnoreCase));
    }

    // G54 — GumIdb is the one RuntimeType that means something different: it reads the .gusx file
    // directly instead of looking the element up in the project. Recognised so it can be reported
    // rather than silently loaded as though it were an ordinary reference.
    [Theory]
    [InlineData("FlatRedBall.Gum.GumIdb", true)]
    [InlineData("Gum.Wireframe.GraphicalUiElement", false)]
    [InlineData("DoorsDemo.GumRuntimes.GameScreenGumRuntime", false)]
    [InlineData(null, false)]
    public void IsLegacyGumIdb_ByRuntimeType(string? runtimeType, bool expected)
    {
        GlueGumResolver.IsLegacyGumIdb(new ReferencedFileSave { RuntimeType = runtimeType })
            .ShouldBe(expected);
    }

    // G52 — SourceType.Gum puts a bare *type name* in SourceFile where every other SourceType puts a
    // real path, so a shared resolver would try to open a file called "StateComponentRuntime". The
    // type name is what identifies the element.
    [Fact]
    public void ComponentElementNameFor_ASourceTypeGumObject_ResolvesByTypeNameNotByPath()
    {
        var save = new NamedObjectSave
        {
            InstanceName = "StateComponentRuntimeInstance",
            SourceClassType = "GlueTestProject.GumRuntimes.StateComponentRuntime",
            SourceType = SourceType.Gum,
            SourceFile = "StateComponentRuntime",
        };

        GlueGumResolver.ComponentElementNameFor(save, "GumProject/GumProject.gumx")
            .ShouldBe("StateComponent");
    }

    [Theory]
    // Glue's Gum codegen names a component's runtime class <Element>Runtime, so the element name is
    // the class minus its namespace and that suffix.
    [InlineData("GlueTestProject.GumRuntimes.StateComponentRuntime", "StateComponent")]
    [InlineData("NineSliceButtonRuntime", "NineSliceButton")]
    // Not every Gum type follows the convention; without the suffix the name stands as-is rather
    // than losing its last eight characters.
    [InlineData("CardGum", "CardGum")]
    [InlineData(null, null)]
    public void ElementNameFromRuntimeType_StripsNamespaceAndRuntimeSuffix(
        string? runtimeType, string? expected)
    {
        GlueGumResolver.ElementNameFromRuntimeType(runtimeType).ShouldBe(expected);
    }

    // The other half of G52: Glue's own recogniser (GumPluginCodeGenerator.IsGue) treats a Gum object
    // sourced from a file as SourceType.File with a .gucx extension, so both shapes must work.
    [Fact]
    public void ComponentElementNameFor_AGucxSourcedFileObject_ResolvesByElementName()
    {
        var save = new NamedObjectSave
        {
            InstanceName = "CardInstance",
            SourceType = SourceType.File,
            SourceFile = "GumProject/Components/Controls/ButtonStandard.gucx",
        };

        GlueGumResolver.ComponentElementNameFor(save, "GumProject/GumProject.gumx")
            .ShouldBe("Controls/ButtonStandard");
    }

    [Fact]
    public void ComponentElementNameFor_AnOrdinaryFlatRedBallObject_IsNotAGumObject()
    {
        var save = new NamedObjectSave
        {
            InstanceName = "CollisionBox",
            SourceClassType = "FlatRedBall.Math.Geometry.AxisAlignedRectangle",
            SourceType = SourceType.FlatRedBallType,
        };

        GlueGumResolver.ComponentElementNameFor(save, "GumProject/GumProject.gumx").ShouldBeNull();
    }

    [Fact]
    public void GumScreenNameFor_AScreenWithNoGumAnywhereInItsChain_ReturnsNull()
    {
        var screen = new ScreenSave { Name = @"Screens\Bare" };

        GlueGumResolver.GumElementNameFor(screen, new GlueProjectSave(), "GumProject/GumProject.gumx")
            .ShouldBeNull();
    }
}
