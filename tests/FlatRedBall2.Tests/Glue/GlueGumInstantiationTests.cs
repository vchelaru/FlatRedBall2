using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using Gum.Managers;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

/// <summary>
/// One engine booted from a Glue project, shared by the tests in the class that owns this fixture.
/// </summary>
/// <remarks>
/// Gum keeps its project and managers in process-wide statics, so only one engine may hold them at
/// a time. This is a class fixture rather than a collection one for exactly that reason: it owns
/// them for the lifetime of one test class and releases them in <see cref="Dispose"/>, leaving the
/// process clean for anything else that needs an engine.
/// </remarks>
public sealed class GlueGumFixture : IDisposable
{
    public GlueGumFixture()
    {
        StageFixtureContent();

        try
        {
            _game = new Game();
            _ = new GraphicsDeviceManager(_game)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
            };
            _game.RunOneFrame();
        }
        catch (Exception e)
        {
            // No display, no driver, or a headless agent — same contract as GraphicsDeviceFixture.
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            _game?.Dispose();
            _game = null;
            return;
        }

        // The whole-project overload — the one games are told to use.
        Service = new FlatRedBallService();
        Service.Initialize(_game, Path.Combine("Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));
    }

    private Game? _game;

    /// <summary>The engine, or null when this machine cannot provide a device.</summary>
    public FlatRedBallService? Service { get; }

    public bool IsAvailable => Service is not null;

    public static string FixtureDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo");

    /// <summary>
    /// Stages the fixture's Gum project next to the test binary, because Gum's FileManager resolves
    /// every path against the app's own Content folder rather than the Glue project's. A real game
    /// needs no equivalent: its Content folder *is* the Glue project's.
    /// </summary>
    private static void StageFixtureContent()
    {
        CopyDirectory(
            Path.Combine(FixtureDirectory, "Content", "GumProject"),
            Path.Combine(AppContext.BaseDirectory, "Content", "GumProject"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    public void Dispose()
    {
        // Releases Gum's process-wide statics, so the next class to want an engine can have one.
        Service?.Shutdown();
        _game?.Dispose();
    }
}

// Covers actually showing a loaded project's UI, which needs a Gum runtime and so a real device.
// In GraphicsDeviceCollection so this does not run in parallel with anything else that builds a
// Game — that combination fails intermittently.
[Collection(GraphicsDeviceCollection.Name)]
public class GlueGumInstantiationTests : IClassFixture<GlueGumFixture>
{
    private readonly GlueGumFixture _fixture;

    public GlueGumInstantiationTests(GlueGumFixture fixture) => _fixture = fixture;

    [Fact]
    public void Initialize_WithAGlueProject_LoadsTheGumProjectItReferences()
    {
        if (!_fixture.IsAvailable)
            return;

        // Gum only works with a project it loaded itself (G57), so the proof is that Gum's own
        // finder has it — not merely that the loader found the path.
        ObjectFinder.Self.GumProjectSave.ShouldNotBeNull();
        ObjectFinder.Self.GumProjectSave!.Screens.ShouldContain(s => s.Name == "GameScreenGum");
    }

    // Naming the .gluj is the whole boot: the project loads, its start-up screen runs, and the
    // screen is given the data it needs. None of that is information the caller has and the engine
    // does not, so none of it should have to be passed back in.
    [Fact]
    public void Initialize_WithAGlueProjectPath_StartsItsStartUpScreenReadyToBuild()
    {
        if (!_fixture.IsAvailable)
            return;

        var project = _fixture.Service!.GlueProject.ShouldNotBeNull();
        var current = _fixture.Service.CurrentScreen.ShouldBeOfType<GlueScreen>();

        current.Project.ShouldBeSameAs(project);
        current.Save.ShouldBeSameAs(project.StartUpScreen);
    }

    // The project's own display block is what sizes the window, and nothing but this path applies
    // it — GlueProject.ApplyDisplaySettings had no caller at all.
    [Fact]
    public void Initialize_WithAGlueProjectPath_AppliesTheProjectsDisplaySettings()
    {
        if (!_fixture.IsAvailable)
            return;

        // DoorsDemo's DisplaySettings block declares 256x224, not the engine's 1280x720 default.
        _fixture.Service!.DisplaySettings.ResolutionWidth.ShouldBe(256);
        _fixture.Service.DisplaySettings.ResolutionHeight.ShouldBe(224);
    }

    [Fact]
    public void Initialize_WithAGlueProject_ExposesItSoTheCallerNeedNotLoadItTwice()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;

        service.GlueProject.ShouldNotBeNull();
        service.GlueProject!.StartUpScreen!.Name.ShouldBe(@"Screens\Level1");
        service.GlueProject.Result.GumProjectFile.ShouldBe("GumProject/GumProject.gumx");
        // Content comes wired to the engine's loader, so assets resolve without the caller building
        // a source by hand.
        service.GlueProject.Content.ShouldNotBeNull();
    }

    // The service builds the project's content source itself, and every other test hands
    // GlueContentSource a device by hand — so nothing noticed when that one did not. Without the
    // device the source skips every referenced .tmx and says so only in a diagnostic nobody reads,
    // which reads as a screen that loads fine and draws nothing.
    [Fact]
    public void Initialize_WithAGlueProject_BuildsTheTileMapsItsScreensReference()
    {
        if (!_fixture.IsAvailable)
            return;

        var screen = _fixture.Service!.GlueProject!.CreateScreen(@"Screens\Level1");
        screen.BuildObjects();

        // The device check runs before the file is opened, so its absence is the whole signal. This
        // fixture keeps its content under Content/ rather than beside the .gluj, so the map itself
        // still does not resolve here — GlueTiledTests covers a map that loads.
        screen.BuildDiagnostics.ShouldNotContain(d => d.Message.Contains("graphics device"));
    }

    [Fact]
    public void Start_AGlueScreenDeclaringAGumScreen_ShowsIt()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;
        var gameScreen = service.GlueProject!.Result.Project.Screens
            .Single(s => s.Name == @"Screens\GameScreen");

        service.Start<GlueScreen>(screen =>
        {
            screen.Save = gameScreen;
            screen.Project = service.GlueProject;
        });

        var current = (GlueScreen)service.CurrentScreen;
        current.GumScreen.ShouldNotBeNull();
        current.GumScreen!.Children.ShouldNotBeEmpty();
    }

    // G53 — Level1 is the project's StartUpScreen and declares no .gusx of its own, so the
    // inheriting case is the primary boot path rather than an edge case.
    [Fact]
    public void Start_TheStartUpScreen_ShowsTheGumScreenItInherits()
    {
        if (!_fixture.IsAvailable)
            return;

        var service = _fixture.Service!;

        service.Start<GlueScreen>(screen =>
        {
            screen.Save = service.GlueProject!.StartUpScreen;
            screen.Project = service.GlueProject;
        });

        var current = (GlueScreen)service.CurrentScreen;
        current.GlueName.ShouldBe(@"Screens\Level1");
        current.GumScreen.ShouldNotBeNull();
    }
}
