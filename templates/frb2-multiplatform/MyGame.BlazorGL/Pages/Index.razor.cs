using System;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace MyGame.BlazorGL.Pages;

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

    private Game? _game;

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
            _game = GameFactory();
            _game.Run();
        }
        _game.Tick();
    }
}
