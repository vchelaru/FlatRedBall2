using System;
using FlatRedBall2.Tests.Glue;
using Gum.Managers;
using Microsoft.Xna.Framework;
using RenderingLibrary;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

/// <summary>
/// Covers standing an engine back down, which is what lets a second one exist in the same process.
/// </summary>
/// <remarks>
/// Gum keeps its project, managers and canvas size in process-wide statics, so two engines
/// initialized at once fight over them — the reason every Gum-touching test has had to share one
/// service. Gum has always supported being re-initialized after a teardown; FRB2 simply never tore
/// down. In <see cref="GraphicsDeviceCollection"/> because it builds a <c>Game</c>, and skipped when
/// another fixture in that collection already owns Gum: initializing on top of a live instance is
/// the very thing this is about.
/// </remarks>
[Collection(GraphicsDeviceCollection.Name)]
public class ServiceShutdownTests
{
    private static bool GumIsOwnedElsewhere => SystemManagers.Default is not null;

    private static Game? TryCreateGame()
    {
        try
        {
            var game = new Game();
            _ = new GraphicsDeviceManager(game) { PreferredBackBufferWidth = 64, PreferredBackBufferHeight = 64 };
            game.RunOneFrame();
            return game;
        }
        catch (Exception e)
        {
            // No display, no driver, or a headless agent — same contract as GraphicsDeviceFixture.
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            return null;
        }
    }

    [Fact]
    public void Shutdown_AfterInitialize_ReleasesTheProcessWideGumState()
    {
        if (GumIsOwnedElsewhere)
            return;

        using var game = TryCreateGame();
        if (game is null)
            return;

        var engine = new FlatRedBallService();
        engine.Initialize(game);
        SystemManagers.Default.ShouldNotBeNull();

        engine.Shutdown();

        // The statics a second engine would otherwise inherit from the first.
        SystemManagers.Default.ShouldBeNull();
        ObjectFinder.Self.GumProjectSave.ShouldBeNull();
    }

    // The point of the teardown: a second engine in the same process, which is what every
    // Gum-touching test needs before it can own its own service instead of sharing one.
    [Fact]
    public void Initialize_AfterAPreviousEngineShutDown_Succeeds()
    {
        if (GumIsOwnedElsewhere)
            return;

        using var game = TryCreateGame();
        if (game is null)
            return;

        var first = new FlatRedBallService();
        first.Initialize(game);
        first.Shutdown();

        var second = new FlatRedBallService();
        Should.NotThrow(() => second.Initialize(game));
        SystemManagers.Default.ShouldNotBeNull();

        second.Shutdown();
    }
}
