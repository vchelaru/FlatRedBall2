using System;
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

    private Game? _game;

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
        if (_game == null)
        {
            _game = GameFactory();
            _game.Run();
        }
        _game.Tick();
    }
}
