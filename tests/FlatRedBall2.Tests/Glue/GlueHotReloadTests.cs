using System;
using System.Collections.Generic;
using System.IO;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

/// <summary>
/// Glue writes into the project's source tree; the game reads the build output. These cover the
/// watch → copy → reparse → restart chain that closes that gap.
/// </summary>
public class GlueHotReloadTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _srcRoot;
    private readonly string _destRoot;

    public GlueHotReloadTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "frb2-glue-hotreload-" + Guid.NewGuid().ToString("N"));
        _srcRoot = Path.Combine(_tempRoot, "src");
        _destRoot = Path.Combine(_tempRoot, "bin");
        Directory.CreateDirectory(_srcRoot);
        Directory.CreateDirectory(_destRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private const string ProjectFileName = "Test.gluj";

    /// <summary>
    /// Writes a .gluj naming one screen, plus that screen's .glsj, into both roots. Mirrors what
    /// MSBuild leaves behind: an identical copy of the source tree in the output folder.
    /// </summary>
    private void WriteProject(string screenName)
    {
        var gluj = $$"""
            { "FileVersion": 68, "StartUpScreen": "{{screenName.Replace("\\", "\\\\")}}",
              "ScreenReferences": [ { "Name": "{{screenName.Replace("\\", "\\\\")}}" } ] }
            """;
        var glsj = $$"""{ "Name": "{{screenName.Replace("\\", "\\\\")}}" }""";
        foreach (var root in new[] { _srcRoot, _destRoot })
        {
            Directory.CreateDirectory(Path.Combine(root, "Screens"));
            File.WriteAllText(Path.Combine(root, ProjectFileName), gluj);
            File.WriteAllText(
                Path.Combine(root, "Screens", Path.GetFileName(screenName.Replace('\\', '/')) + ".glsj"),
                glsj);
        }
    }

    private FlatRedBallService MakeEngine()
    {
        var engine = new FlatRedBallService { OutputContentRoot = _destRoot };
        engine.SourceContentRoots.Clear();
        engine.SourceContentRoots.Add(_srcRoot);
        engine.LoadGlueProject(ProjectFileName);
        return engine;
    }

    /// <summary>
    /// A restart is queued during the watcher tick and applied at the top of the next frame, so
    /// seeing the restarted screen takes two updates.
    /// </summary>
    private static void UpdateTwice(FlatRedBallService engine)
    {
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        engine.Update(new Microsoft.Xna.Framework.GameTime());
    }

    private static GlueScreen StartGlueScreen(FlatRedBallService engine)
    {
        // Captures the project in a local exactly as a game's Game1 does. The engine replays this
        // callback on restart, so the captured instance is what a naive restart would rebuild from
        // - which is the whole reason hot reload has to supply its own configure.
        var project = engine.GlueProject!;
        engine.Start<GlueScreen>(s =>
        {
            s.Project = project;
            s.Save = project.StartUpScreen;
        });
        return (GlueScreen)engine.CurrentScreen;
    }

    [Fact]
    public void CustomInitialize_WithGlueProject_WatchesTheProjectDirectory()
    {
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();

        var screen = StartGlueScreen(engine);

        // One watcher rooted at the .gluj's own directory - Glue writes element files beside it
        // (Screens/, Entities/) as well as assets under Content/.
        screen.ContentDirectoryWatchers.Count.ShouldBe(1);
    }

    [Fact]
    public void CustomInitialize_WithASourceRootThatHasNoGlueProject_DoesNotWatchIt()
    {
        // The watch is rooted at a project directory rather than at Content/, so "every root that
        // contains this directory" is every root there is. Running a sample from source is enough to
        // hit this: the engine's own project is a detected source root, and would get a recursive
        // watcher over the whole engine tree.
        WriteProject(@"Screens\Level1");
        var unrelatedRoot = Path.Combine(_tempRoot, "engine");
        Directory.CreateDirectory(unrelatedRoot);
        var engine = MakeEngine();
        engine.SourceContentRoots.Insert(0, unrelatedRoot);

        var screen = StartGlueScreen(engine);

        screen.ContentDirectoryWatchers.Count.ShouldBe(1);
    }

    [Fact]
    public void CustomInitialize_WithGlueHotReloadDisabled_RegistersNoWatcher()
    {
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        engine.IsGlueHotReloadEnabled = false;

        var screen = StartGlueScreen(engine);

        screen.ContentDirectoryWatchers.ShouldBeEmpty();
    }

    [Fact]
    public void ReloadGlueProject_AfterSourceEdit_ReplacesTheLoadedProject()
    {
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var before = engine.GlueProject;

        // Glue renames the screen; the game still reads the output copy, so write there.
        File.WriteAllText(
            Path.Combine(_destRoot, ProjectFileName),
            """
            { "FileVersion": 68, "StartUpScreen": "Screens\\Level2",
              "ScreenReferences": [ { "Name": "Screens\\Level2" } ] }
            """);
        File.WriteAllText(Path.Combine(_destRoot, "Screens", "Level2.glsj"), """{ "Name": "Screens\\Level2" }""");

        engine.ReloadGlueProject().ShouldBeTrue();

        engine.GlueProject.ShouldNotBe(before);
        engine.GlueProject!.StartUpScreen!.Name.ShouldBe(@"Screens\Level2");
    }

    [Fact]
    public void LoadGlueProject_RelativePath_ReadsFromOutputContentRootNotTheWorkingDirectory()
    {
        // The working directory is the project folder under `dotnet run` and the output folder under
        // a debugger, so a plain relative read finds a different (stale) project depending on how the
        // game was launched. Only the output copy is ever the one the game is running.
        WriteProject(@"Screens\Level1");
        File.WriteAllText(
            Path.Combine(_destRoot, ProjectFileName),
            """
            { "FileVersion": 68, "StartUpScreen": "Screens\\Level1",
              "ScreenReferences": [ { "Name": "Screens\\Level1" } ] }
            """);
        // Only the output copy names Level1's NextScreen, so reading it proves which copy was read.
        File.WriteAllText(
            Path.Combine(_destRoot, "Screens", "Level1.glsj"),
            """{ "Name": "Screens\\Level1", "NextScreen": "Screens\\FromOutput" }""");

        var engine = MakeEngine();

        engine.GlueProject!.StartUpScreen!.NextScreen.ShouldBe(@"Screens\FromOutput");
        engine.GlueProject.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ReloadGlueProject_WithNoGlueProjectFile_ReturnsFalse()
    {
        var engine = new FlatRedBallService();

        engine.ReloadGlueProject().ShouldBeFalse();
    }

    [Fact]
    public void GlueFileChanged_CopiesSourceToOutput_ThenRestartsOnTheReloadedScreen()
    {
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        // Glue adds an object to Level1 and saves. Only the source tree changes.
        File.WriteAllText(
            Path.Combine(_srcRoot, "Screens", "Level1.glsj"),
            """{ "Name": "Screens\\Level1", "NextScreen": "Screens\\Level2" }""");

        watcher.MarkChangedAt(Path.Combine("Screens", "Level1.glsj"), DateTime.UtcNow - TimeSpan.FromSeconds(1));
        UpdateTwice(engine);

        // The edit reached the output copy, was reparsed, and the restarted screen is the same
        // Glue screen rebuilt from the new data - not a stale ScreenSave from the first load.
        File.ReadAllText(Path.Combine(_destRoot, "Screens", "Level1.glsj")).ShouldContain("Level2");
        var restarted = (GlueScreen)engine.CurrentScreen;
        restarted.ShouldNotBe(screen);
        restarted.GlueName.ShouldBe(@"Screens\Level1");
        restarted.Save!.NextScreen.ShouldBe(@"Screens\Level2");
    }

    [Fact]
    public void NewGlueElementFile_NotYetInOutput_IsStillCopiedAndReloaded()
    {
        // A screen added in Glue has no build-output copy yet. The dest-exists gate would normally
        // filter it, which would make "add a screen in Glue" the one edit hot reload can't see.
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        File.WriteAllText(Path.Combine(_srcRoot, "Screens", "Level2.glsj"), """{ "Name": "Screens\\Level2" }""");

        watcher.MarkChangedAt(Path.Combine("Screens", "Level2.glsj"), DateTime.UtcNow - TimeSpan.FromSeconds(1));
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.Exists(Path.Combine(_destRoot, "Screens", "Level2.glsj")).ShouldBeTrue();
    }

    [Fact]
    public void OneSaveTouchingSeveralFiles_ReparsesOnlyAfterEveryCopyLands()
    {
        // Adding a screen in Glue rewrites the .gluj and writes a new .glsj in the same save. The
        // watcher copies one file at a time, so reparsing on the first callback would read a tree
        // where the .gluj names a screen whose file has not been copied yet.
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        File.WriteAllText(
            Path.Combine(_srcRoot, ProjectFileName),
            """
            { "FileVersion": 68, "StartUpScreen": "Screens\\Level1",
              "ScreenReferences": [ { "Name": "Screens\\Level1" }, { "Name": "Screens\\Level2" } ] }
            """);
        File.WriteAllText(Path.Combine(_srcRoot, "Screens", "Level2.glsj"), """{ "Name": "Screens\\Level2" }""");

        var t0 = DateTime.UtcNow - TimeSpan.FromSeconds(1);
        watcher.MarkChangedAt(ProjectFileName, t0);
        watcher.MarkChangedAt(Path.Combine("Screens", "Level2.glsj"), t0);
        UpdateTwice(engine);

        engine.GlueProject!.FindScreen(@"Screens\Level2").ShouldNotBeNull();
    }

    [Fact]
    public void NewTmxAsset_NotYetInOutput_IsStillCopied()
    {
        // Glue adds referenced assets to Content/ the same way it adds element files. The
        // dest-exists gate would hold them back until a rebuild.
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        var rel = Path.Combine("Content", "Level1.tmx");
        Directory.CreateDirectory(Path.Combine(_srcRoot, "Content"));
        File.WriteAllText(Path.Combine(_srcRoot, rel), "<map/>");

        watcher.MarkChangedAt(rel, DateTime.UtcNow - TimeSpan.FromSeconds(1));
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        File.ReadAllText(Path.Combine(_destRoot, rel)).ShouldBe("<map/>");
    }

    [Fact]
    public void BuildIntermediateChanged_IsIgnored()
    {
        // A background `dotnet build` writes into obj/ and bin/ constantly. Those are never content,
        // and reacting to them would restart the screen on every keystroke-triggered compile.
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        var objFile = Path.Combine(_srcRoot, "obj", "Debug", "Test.gluj");
        Directory.CreateDirectory(Path.GetDirectoryName(objFile)!);
        File.WriteAllText(objFile, "{}");

        watcher.MarkChangedAt(Path.Combine("obj", "Debug", "Test.gluj"), DateTime.UtcNow - TimeSpan.FromSeconds(1));
        UpdateTwice(engine);

        engine.CurrentScreen.ShouldBe(screen);
    }

    [Fact]
    public void GumFileChanged_IsLeftToGumsOwnPipeline()
    {
        // Gum runs its own in-place hot reload. Restarting the screen underneath it would tear down
        // the very visuals it just patched.
        WriteProject(@"Screens\Level1");
        var engine = MakeEngine();
        var screen = StartGlueScreen(engine);
        var watcher = screen.ContentDirectoryWatchers[0];
        watcher.Debounce = TimeSpan.Zero;

        var gusj = Path.Combine("Content", "GumProject", "Screens", "MainMenu.gusj");
        foreach (var root in new[] { _srcRoot, _destRoot })
        {
            Directory.CreateDirectory(Path.Combine(root, Path.GetDirectoryName(gusj)!));
            File.WriteAllText(Path.Combine(root, gusj), "{}");
        }

        watcher.MarkChangedAt(gusj, DateTime.UtcNow - TimeSpan.FromSeconds(1));
        UpdateTwice(engine);

        engine.CurrentScreen.ShouldBe(screen);
    }
}
