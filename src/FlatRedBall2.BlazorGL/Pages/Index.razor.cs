using System;
using FlatRedBall2.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.BlazorGL.Pages;

/// <summary>
/// Hosts the FlatRedBall2 game inside a Blazor WebAssembly page. Wires the JS-side
/// requestAnimationFrame loop ("tickJS") into the .NET-side <see cref="Game.Tick"/> call.
/// </summary>
/// <remarks>
/// The consumer registers a <see cref="Func{Game}"/> in DI; this component resolves it on the
/// first tick to construct the game instance. The factory pattern keeps the host generic
/// across games while letting each consumer pick their own <see cref="Game"/> subclass.
/// </remarks>
public partial class Index
{
    /// <summary>
    /// Factory that constructs the <see cref="Game"/> instance on the first tick. Registered
    /// by the consumer with <c>builder.Services.AddSingleton&lt;Func&lt;Game&gt;&gt;(_ =&gt; () =&gt; new MyGame())</c>.
    /// </summary>
    [Inject] public Func<Game> GameFactory { get; set; } = default!;

    /// <summary>Source of the page URL, read once for the <c>?diag=perf</c> opt-in.</summary>
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private Game? _game;
    private PerfDiagnosticsProbe? _perfProbe;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (firstRender)
        {
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
        }
    }

    /// <summary>
    /// Called from JS once per animation frame. Lazy-constructs the <see cref="Game"/> from
    /// the injected factory on the first tick, then drives <see cref="Game.Tick"/>.
    /// </summary>
    [JSInvokable]
    public void TickDotNet()
    {
        var isFirstTick = _game == null;
        if (isFirstTick)
        {
            // Before the game exists so the diagnostics are on from its first frame.
            _perfProbe = PerfDiagnosticsProbe.TryCreateFromQuery(
                new Uri(Navigation.Uri).Query, FlatRedBallService.Default, GetBrowserLabel,
                Console.WriteLine, PublishToJs);
            // -= first: a factory that threw leaves _game null, so the next tick retries from here.
            FlatRedBallService.Default.StartupTiming.PhaseStarted -= PublishBootPhase;
            FlatRedBallService.Default.StartupTiming.PhaseStarted += PublishBootPhase;
            try
            {
                _game = GameFactory();
                _game.Run();
            }
            catch (Exception ex)
            {
                InvokeBootScreen("frbBootFailed", $"{ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
        _game!.Tick();
        _perfProbe?.OnTick();
        // Boot is over once the first frame has run; later phases ("Screen load" on a screen
        // change) are not boot progress.
        if (isFirstTick)
            FlatRedBallService.Default.StartupTiming.PhaseStarted -= PublishBootPhase;
    }

    // frbBootPhase / frbBootFailed are defined by boot-screen.js, which a project opts into with
    // one <script> tag. Without it the first call throws once, and the sink disables itself.
    private void PublishBootPhase(string phaseName) => InvokeBootScreen("frbBootPhase", phaseName);

    private bool _bootScreenUnavailable;

    private void InvokeBootScreen(string function, params object?[] args)
    {
        if (_bootScreenUnavailable) return;
        try { (JsRuntime as IJSInProcessRuntime)?.InvokeVoid(function, args); }
        catch (JSException) { _bootScreenUnavailable = true; }
    }

    // frbDiagPlatform / frbDiagPublish live in frb-host.js. A project that vendored an older copy
    // lacks them; the probe still reports to the console, just without a browser name.
    private string? GetBrowserLabel()
    {
        try { return (JsRuntime as IJSInProcessRuntime)?.Invoke<string?>("frbDiagPlatform"); }
        catch (JSException) { return null; }
    }

    private void PublishToJs(string json)
    {
        try { (JsRuntime as IJSInProcessRuntime)?.InvokeVoid("frbDiagPublish", json); }
        catch (JSException) { }
    }
}
