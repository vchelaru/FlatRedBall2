using System;
using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

/// <summary>
/// A <see cref="Game"/> whose Update/Draw call through to <see cref="Engine"/>, matching how a real
/// Game1 wires <see cref="FlatRedBallService"/>. Engine is null until a test attaches one.
/// </summary>
/// <remarks>
/// Disposing a Game tears down process-wide GL/SDL state that every other test's device depends on,
/// and repeatedly creating and disposing Games has been a suspect in intermittent native crashes of
/// the Linux test host (#1300). So the run owns exactly one, and tests attach and detach an engine.
/// </remarks>
public sealed class SharedGame : Game
{
    public FlatRedBallService? Engine;
    protected override void Update(GameTime gameTime) => Engine?.Update(gameTime);
    protected override void Draw(GameTime gameTime) => Engine?.Draw();
}

/// <summary>
/// A real <see cref="GraphicsDevice"/> for tests that genuinely need one — loading a texture, or
/// anything else that cannot be faked.
/// </summary>
/// <remarks>
/// Creating a device needs a GL context, not merely a GPU, so this only works on a machine with a
/// display. <see cref="IsAvailable"/> is false on a headless CI agent; tests that need a device
/// should skip rather than fail there.
/// <para>Shared through <see cref="GraphicsDeviceCollection"/> so one device serves the whole run —
/// creating one per test class is slow and risks contending for the context.</para>
/// </remarks>
public sealed class GraphicsDeviceFixture : IDisposable
{
    private readonly SharedGame? _game;

    public GraphicsDeviceFixture()
    {
        try
        {
            _game = new SharedGame();

            // The manager has to exist before RunOneFrame, which is what actually creates the
            // device — without entering a message loop, which a test host cannot pump.
            _ = new GraphicsDeviceManager(_game)
            {
                PreferredBackBufferWidth = 64,
                PreferredBackBufferHeight = 64,
            };

            _game.RunOneFrame();
            GraphicsDevice = _game.GraphicsDevice;

            var content = new ContentLoader();
            content.Initialize(_game.Content, GraphicsDevice);
            ContentLoader = content;
        }
        catch (Exception e)
        {
            // No display, no driver, or a headless agent. Tests check IsAvailable and skip.
            System.Diagnostics.Debug.WriteLine($"[tests] No graphics device available: {e.Message}");
            _game?.Dispose();
            _game = null;
        }
    }

    /// <summary>
    /// The one <see cref="Game"/> for the whole run, or null when this machine cannot provide one.
    /// Tests that need a running game borrow this instead of building their own; never dispose it.
    /// </summary>
    public SharedGame? Game => _game;

    /// <summary>The device, or null when this machine cannot provide one.</summary>
    public GraphicsDevice? GraphicsDevice { get; }

    /// <summary>A content loader bound to that device, for tests that load real assets.</summary>
    public ContentLoader? ContentLoader { get; }

    /// <summary>Whether tests needing a real device can run here.</summary>
    public bool IsAvailable => GraphicsDevice is not null;

    public void Dispose() => _game?.Dispose();
}

/// <summary>Shares one <see cref="GraphicsDeviceFixture"/> across every test class that opts in.</summary>
/// <remarks>
/// Membership also serializes these classes against each other, which matters beyond sharing the
/// device: a second <c>Game</c> or a second Gum initialization running concurrently in another
/// collection fails intermittently. Anything that builds a <c>Game</c> belongs in this collection.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class GraphicsDeviceCollection
    : ICollectionFixture<GraphicsDeviceFixture>
{
    public const string Name = "GraphicsDevice";
}

[Collection(GraphicsDeviceCollection.Name)]
public class GraphicsDeviceFixtureTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public GraphicsDeviceFixtureTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    [Fact]
    public void GraphicsDevice_OnAMachineWithADisplay_CanCreateATexture()
    {
        // Guards the fixture itself: if device creation regresses, every asset-loading test would
        // otherwise skip silently and look green.
        if (!_fixture.IsAvailable)
            return;

        var texture = new Texture2D(_fixture.GraphicsDevice!, 2, 2);

        texture.Width.ShouldBe(2);
        texture.Height.ShouldBe(2);
    }
}
