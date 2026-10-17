using System;
using FlatRedBall2;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace GameNamespace.BlazorGL.Pages;

/// <summary>
/// Hosts the FlatRedBall2 game inside a Blazor WebAssembly page. Wires the JS-side
/// requestAnimationFrame loop ("tickJS" in wwwroot/frb-host.js) into the .NET-side
/// <see cref="Game.Tick"/> call.
/// </summary>
/// <remarks>
/// Game construction is deferred until the first tick: <see cref="GameFactory"/> is
/// resolved from DI (registered in Program.cs). This file is yours — edit freely to add
/// custom JS interop, alternate canvas behaviors, or a different page layout.
/// </remarks>
public partial class Index
{
    [Inject] public Func<Game> GameFactory { get; set; } = default!;

    /// <summary>Source of the page URL, read once for the <c>?diag=perf</c> opt-in.</summary>
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private Game? _game;
    private PerfDiagnosticsProbe? _perfProbe;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (firstRender)
        {
            JsRuntime.InvokeAsync<object>("initRenderJS", DotNetObjectReference.Create(this));
        }
    }

    /// <summary>Called from JS once per animation frame.</summary>
    [JSInvokable]
    public void TickDotNet()
    {
        if (_game == null)
        {
            // Before the game exists so the diagnostics are on from its first frame.
            _perfProbe = PerfDiagnosticsProbe.TryCreateFromQuery(
                new Uri(Navigation.Uri).Query, FlatRedBallService.Default, GetBrowserLabel,
                Console.WriteLine, PublishToJs);
            // localStorage-backed Engine.Storage; Initialize calls this with EngineInitSettings.StorageName.
            FlatRedBallService.Default.StorageBackendFactory =
                name => new BrowserStorageBackend(name, new LocalStorageApi());
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
    }

    // frbBootFailed is defined by boot-screen.js, which index.html opts into with one <script> tag.
    // Without it the first call throws once, and this reporting disables itself.

    private bool _bootScreenUnavailable;

    private void InvokeBootScreen(string function, params object?[] args)
    {
        if (_bootScreenUnavailable) return;
        try { (JsRuntime as IJSInProcessRuntime)?.InvokeVoid(function, args); }
        catch (JSException) { _bootScreenUnavailable = true; }
    }

    // frbDiagPlatform / frbDiagPublish live in wwwroot/frb-host.js.
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
