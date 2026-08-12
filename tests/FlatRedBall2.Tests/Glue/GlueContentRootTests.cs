using System;
using System.IO;
using System.Collections.Generic;
using FlatRedBall2.Content;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Where a Glue project's referenced files resolve from. The editor keeps its project in its own
// folder (Content/FrbEditor/) so a game can copy one directory to output, which means the .gluj no
// longer sits beside Content/ - and the content root must not follow it there.
public class GlueContentRootTests : IDisposable
{
    private readonly string _output;

    public GlueContentRootTests()
    {
        _output = Path.Combine(Path.GetTempPath(), "Frb2ContentRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_output);
    }

    public void Dispose()
    {
        try { Directory.Delete(_output, recursive: true); } catch (IOException) { }
    }

    private const string MinimalGluj = @"{ ""FileVersion"": 68 }";

    private string WriteGluj(string relativePath)
    {
        var full = Path.Combine(_output, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, MinimalGluj);
        // Callers pass forward slashes, which is what a game hands to GlueProjectFile.
        return relativePath;
    }

    // The editor keeps a project's .gluj and everything it references in one folder, so a referenced
    // file resolves relative to the .gluj itself - no fixed "Content" segment in between. That is what
    // lets a game copy the one folder to output and lets a user delete it to remove the editor's
    // footprint entirely.

    [Fact]
    public void LoadGlueProject_GlujAtTheOutputRoot_ResolvesContentBesideIt()
    {
        var service = new FlatRedBallService { OutputContentRoot = _output };

        service.LoadGlueProject(WriteGluj("MyGame.gluj"));

        service.GlueProject!.Content!.ContentRoot.ShouldBe(string.Empty);
    }

    [Fact]
    public void LoadGlueProject_GlujInItsOwnFolder_ResolvesContentInsideThatFolder()
    {
        var service = new FlatRedBallService { OutputContentRoot = _output };

        service.LoadGlueProject(WriteGluj("Content/FrbEditor/MyGame.gluj"));

        // Not the output root: a file the project references is at
        // Content/FrbEditor/Screens/<screen>/<file>, beside the .gluj rather than under the game's own
        // Content folder.
        service.GlueProject!.Content!.ContentRoot.ShouldBe(Path.Combine("Content", "FrbEditor"));
    }

    [Fact]
    public void Load_AReferencedFileBesideTheGluj_ResolvesWithoutAContentSegment()
    {
        // The rule itself, rather than the root handed to it: names in a Glue project are relative to
        // the content root directly. GlueContentSource used to insert a "Content" segment between the
        // two, which was invisible while the editor happened to keep the .gluj one level above a
        // Content folder, and wrong the moment it kept the project and its content together.
        //
        // Under AppContext.BaseDirectory because ContentLoader reads through TitleContainer, which
        // resolves against the executable's folder and rejects a rooted path.
        var relativeRoot = Path.Combine("Glue", "ContentRootFixture_" + Guid.NewGuid().ToString("N"));
        var absoluteRoot = Path.Combine(AppContext.BaseDirectory, relativeRoot);
        Directory.CreateDirectory(Path.Combine(absoluteRoot, "Entities", "Thing"));
        File.WriteAllText(Path.Combine(absoluteRoot, "Entities", "Thing", "data.csv"), "Name\nRow1");

        try
        {
            var entity = new EntitySave { Name = @"Entities\Thing" };
            entity.ReferencedFiles.Add(new ReferencedFileSave
            {
                Name = "Entities/Thing/data.csv",
                LoadedAtRuntime = true,
            });

            var source = new GlueContentSource(new ContentLoader(), relativeRoot);
            source.Load(entity, new List<GlueLoadDiagnostic>());

            source.GetText("data").ShouldNotBeNull();
        }
        finally
        {
            try { Directory.Delete(absoluteRoot, recursive: true); } catch (IOException) { }
        }
    }
}
