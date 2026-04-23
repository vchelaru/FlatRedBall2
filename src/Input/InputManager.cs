using System;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

/// <summary>
/// Top-level input access point: keyboard, mouse/touch cursor, and up to four gamepads.
/// One instance lives on the engine and is exposed as <c>Engine.Input</c>; all input state
/// is captured once per frame before entity and screen logic runs.
/// </summary>
public class InputManager
{
    private readonly Keyboard _keyboard = new Keyboard();
    private readonly Cursor _cursor = new Cursor();
    private readonly Gamepad[] _gamepads;

    /// <summary>
    /// Creates the input manager and allocates four gamepad slots. Called once by the engine
    /// during startup; game code does not construct this directly.
    /// </summary>
    public InputManager()
    {
        _gamepads = new Gamepad[4];
        for (int i = 0; i < 4; i++)
            _gamepads[i] = new Gamepad(i);
    }

    /// <summary>Keyboard input. State is captured once per frame before entity and screen logic runs.</summary>
    public IKeyboard Keyboard => _keyboard;

    /// <summary>Mouse/touch cursor. <see cref="ICursor.WorldPosition"/> is in Y+ up world space, matching entity coordinates.</summary>
    public ICursor Cursor => _cursor;

    /// <summary>Returns the gamepad at the given index (0–3). Safe to call when no controller is connected — returns zeroed state.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for index values outside 0–3.</exception>
    public IGamepad GetGamepad(int index)
    {
        if (index < 0 || index > 3)
            throw new ArgumentOutOfRangeException(nameof(index), "Gamepad index must be between 0 and 3.");
        return _gamepads[index];
    }

    internal void SetCamera(Camera camera) => _cursor.SetCamera(camera);

    internal void Update()
    {
        _keyboard.Update();
        _cursor.Update();
        foreach (var gp in _gamepads)
            gp.Update();
    }
}
