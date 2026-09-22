using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Default <see cref="IKeyboard"/> implementation backed by MonoGame's
/// <see cref="Microsoft.Xna.Framework.Input.Keyboard"/>. Created and updated by
/// <see cref="InputManager"/> — game code should access keyboard input via
/// <c>Engine.Input.Keyboard</c> rather than constructing this directly.
/// </summary>
public class Keyboard : IKeyboard
{
    private KeyboardState _current;
    private KeyboardState _previous;
    private bool _hasInjection;
    private readonly HashSet<Keys> _injectedKeys = new();

    // Keys held at the moment SuppressHeldReleases() was called (e.g. a screen transition) — each
    // one's next release must not be reported, since that press belongs to whichever screen was
    // active when it started. Populated into _releaseSuppressedThisFrame, and removed from this
    // set, the frame its release actually happens.
    private readonly HashSet<Keys> _heldAtSuppression = new();
    private readonly HashSet<Keys> _releaseSuppressedThisFrame = new();

    internal void InjectKey(Keys key, bool down)
    {
        _hasInjection = true;
        if (down) _injectedKeys.Add(key);
        else _injectedKeys.Remove(key);
    }

    /// <summary>
    /// Marks every key currently down so its next release is not reported through
    /// <see cref="WasKeyJustReleased"/>. Called by the engine on every screen transition; a key
    /// that was already up is unaffected, and the very next fresh press/release cycle after the
    /// suppressed release behaves normally again.
    /// </summary>
    internal void SuppressHeldReleases()
    {
        foreach (var key in _current.GetPressedKeys())
            _heldAtSuppression.Add(key);
    }

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update()
    {
        _previous = _current;
        _current = _hasInjection
            ? new KeyboardState(_injectedKeys.ToArray())
            : Microsoft.Xna.Framework.Input.Keyboard.GetState();

        _releaseSuppressedThisFrame.Clear();
        if (_heldAtSuppression.Count > 0)
        {
            foreach (var key in _previous.GetPressedKeys())
            {
                if (_heldAtSuppression.Contains(key) && !_current.IsKeyDown(key))
                {
                    _releaseSuppressedThisFrame.Add(key);
                    _heldAtSuppression.Remove(key);
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsKeyDown(Keys key) => _current.IsKeyDown(key);

    /// <inheritdoc/>
    public bool WasKeyPressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);

    /// <inheritdoc/>
    public bool WasKeyJustReleased(Keys key) =>
        _previous.IsKeyDown(key) && !_current.IsKeyDown(key) && !_releaseSuppressedThisFrame.Contains(key);
}
