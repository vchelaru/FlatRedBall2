using System;
using FlatRedBall2.Tests;
using Xunit.Sdk;

namespace FlatRedBall2.GoldenImage;

/// <summary>
/// A <see cref="FlatRedBallService"/> booted against the shared game, so Gum has a real
/// <c>SystemManagers</c> and renderer. Gum is process-wide static state, so dispose it before the
/// test returns: the next scene boots its own.
/// </summary>
/// <remarks>
/// Scenes that draw Gum start their screen with <see cref="Engine"/>.Start and render
/// <c>Engine.CurrentScreen</c> through <see cref="OffscreenRenderer.RenderCameras"/>, which draws with
/// the screen's own cameras (a camera HUD only draws under the camera that owns it).
/// </remarks>
public sealed class GoldenGum : IDisposable
{
    public FlatRedBallService Engine { get; }

    private GoldenGum(FlatRedBallService engine) => Engine = engine;

    public static GoldenGum Boot(GraphicsDeviceFixture fixture)
    {
        // Resolves the missing-device failure first, so the message names the xvfb/Mesa setup.
        GoldenDevice.Require(fixture);

        if (RenderingLibrary.SystemManagers.Default is not null)
            throw new XunitException("Gum is already initialized by an earlier scene that did not dispose its GoldenGum.");

        var engine = new FlatRedBallService();
        engine.Initialize(fixture.Game!);
        return new GoldenGum(engine);
    }

    public void Dispose() => Engine.Shutdown();
}
