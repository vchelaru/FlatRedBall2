using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers loading the assets a Glue element references, and getting them onto the objects that name
// them. A sprite does not reach its texture through SourceType.File — it names a ReferencedFileSave
// by *instance name* inside an ordinary instruction, which is the case that actually matters.
[Collection(GraphicsDeviceCollection.Name)]
public class GlueContentTests
{
    private readonly GraphicsDeviceFixture _graphics;

    public GlueContentTests(GraphicsDeviceFixture graphics) => _graphics = graphics;

    // Relative: the loader reads through TitleContainer, which resolves from the working
    // directory rather than from an absolute path.
    private static string FixtureDirectory(string project) =>
        Path.Combine("Glue", "Fixtures", project, "Content");

    private static EntitySave LoadFixtureEntity(string project, string fileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "Glue", "Fixtures", project, "Entities", fileName)),
            GlueJsonContext.Default.EntitySave)!;

    private GlueContentSource? SourceFor(string project)
    {
        if (!_graphics.IsAvailable)
            return null;

        return new GlueContentSource(_graphics.ContentLoader!, FixtureDirectory(project));
    }

    [Fact]
    public void Deserialize_ReferencedFileOmittingLoadedAtRuntime_DefaultsToLoading()
    {
        // FRB1 defaults this true and Glue omits defaults, so `true` never appears on disk. Reading
        // it as false means every asset in every real project silently stops loading.
        string json = @"{ ""Name"": ""Entities/Door/Anim.achx"" }";

        var file = JsonSerializer.Deserialize(json, GlueJsonContext.Default.ReferencedFileSave)!;

        file.LoadedAtRuntime.ShouldBeTrue();
        file.DestroyOnUnload.ShouldBeTrue();
        file.IsSharedStatic.ShouldBeTrue();
        file.AddToManagers.ShouldBeTrue();
    }

    [Fact]
    public void InstanceNameOf_StripsPathAndPunctuationTheWayGlueDoes()
    {
        // This transform is the lookup key an instruction value is matched against, so it has to
        // agree with Glue's exactly or nothing resolves.
        GlueContentSource.InstanceNameOf("Entities/Door/AnimationChainListFile.achx")
            .ShouldBe("AnimationChainListFile");
        GlueContentSource.InstanceNameOf("Global/My File (2).png").ShouldBe("MyFile2");
        GlueContentSource.InstanceNameOf("Global/side-scroll.png").ShouldBe("side_scroll");
        GlueContentSource.InstanceNameOf("Global/2ndTexture.png").ShouldBe("_2ndTexture");
    }

    [Fact]
    public void Load_DoorsDemoDoor_ResolvesItsAnimationChainListAndPlaysTheAuthoredChain()
    {
        // Door.glej names its .achx by instance name in an AnimationChains instruction, then names
        // a chain in CurrentChainName — which is a *method* on FRB2's Sprite, not a property.
        var source = SourceFor("DoorsDemo");
        if (source is null)
            return;

        var entity = new GlueEntity
        {
            Save = LoadFixtureEntity("DoorsDemo", "Door.glej"),
            Content = source,
        };

        entity.BuildObjects();

        var sprite = (FlatRedBall2.Rendering.Sprite)entity.Objects["SpriteInstance"];

        sprite.AnimationChains.ShouldNotBeNull();
        // Not `CurrentAnimation?.Name.ShouldBe(...)` — the null-conditional would short-circuit and
        // assert nothing at all when the animation failed to play.
        sprite.CurrentAnimation.ShouldNotBeNull();
        sprite.CurrentAnimation.Name.ShouldBe("Closed");
        entity.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void Load_MissingAsset_WarnsAndLeavesTheRestLoaded()
    {
        var source = SourceFor("DoorsDemo");
        if (source is null)
            return;

        var save = LoadFixtureEntity("DoorsDemo", "Door.glej");
        save.ReferencedFiles.Add(new ReferencedFileSave { Name = "Entities/Door/Gone.achx" });

        var entity = new GlueEntity { Save = save, Content = source };
        entity.BuildObjects();

        entity.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("Gone.achx"));
        entity.Objects.ShouldContainKey("SpriteInstance");
    }

    [Fact]
    public void Load_ContentRootThatCannotBeOpened_WarnsRatherThanKillingTheLoad()
    {
        // An absolute root makes TitleContainer throw ArgumentException rather than an IO error.
        // Letting it escape takes down the whole element load, which breaks the loader's central
        // promise that a bad asset costs you that asset and nothing else.
        if (!_graphics.IsAvailable)
            return;

        var source = new GlueContentSource(
            _graphics.ContentLoader!,
            Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "Content"));

        var entity = new GlueEntity
        {
            Save = LoadFixtureEntity("DoorsDemo", "Door.glej"),
            Content = source,
        };

        Should.NotThrow(() => entity.BuildObjects());

        entity.Objects.ShouldContainKey("SpriteInstance");
        entity.BuildDiagnostics.ShouldContain(d => d.Severity == GlueDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Load_ReferencedFileNotLoadedAtRuntime_IsSkipped()
    {
        var source = SourceFor("DoorsDemo");
        if (source is null)
            return;

        var save = LoadFixtureEntity("DoorsDemo", "Door.glej");
        save.ReferencedFiles.Single().LoadedAtRuntime = false;

        var entity = new GlueEntity { Save = save, Content = source };
        entity.BuildObjects();

        var sprite = (FlatRedBall2.Rendering.Sprite)entity.Objects["SpriteInstance"];

        sprite.AnimationChains.ShouldBeNull();
    }

    [Fact]
    public void Load_CsvReferencedFile_IsAddressableAsText()
    {
        // Phase 4 makes the file available; Phases 11 and 12 parse the rows.
        var source = SourceFor("DoorsDemo");
        if (source is null)
            return;

        var entity = new GlueEntity
        {
            Save = LoadFixtureEntity("DoorsDemo", "Player.glej"),
            Content = source,
        };

        entity.BuildObjects();

        string? csv = entity.Content!.GetText("PlatformerValuesStatic");

        csv.ShouldNotBeNull();
        csv.ShouldContain("MaxSpeedX");
    }

    [Fact]
    public void Load_TextureByInstanceName_ReachesTheSprite()
    {
        var source = SourceFor("DoorsDemo");
        if (source is null)
            return;

        var save = JsonSerializer.Deserialize(@"{
            ""Name"": ""Entities\\Test"",
            ""ReferencedFiles"": [ { ""Name"": ""Entities/Player/frbplatformer.png"",
                                     ""RuntimeType"": ""Microsoft.Xna.Framework.Graphics.Texture2D"" } ],
            ""NamedObjects"": [ {
                ""InstanceName"": ""SpriteInstance"",
                ""SourceClassType"": ""FlatRedBall.Sprite"",
                ""InstructionSaves"": [
                    { ""Type"": ""Texture2D"", ""Member"": ""Texture"", ""Value"": ""frbplatformer"" } ]
            } ]
        }", GlueJsonContext.Default.EntitySave)!;

        var entity = new GlueEntity { Save = save, Content = source };
        entity.BuildObjects();

        var sprite = (FlatRedBall2.Rendering.Sprite)entity.Objects["SpriteInstance"];

        sprite.Texture.ShouldNotBeNull();
        sprite.Texture.ShouldBeOfType<Texture2D>();
    }
}
