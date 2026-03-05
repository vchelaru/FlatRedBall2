---
name: input-system
description: "Input System in FlatRedBall2. Use when working with keyboard, mouse, cursor, gamepad, touch input, key bindings, or input handling. Covers IKeyboard, ICursor, IGamepad, KeyboardInput2D, KeyboardPressableInput, GamepadInput2D, GamepadPressableInput, and I2DInput/IPressableInput interfaces."
---

# Input System in FlatRedBall2

All input is accessed through `Engine.InputManager` from inside an entity. The input manager exposes keyboard, cursor (mouse/touch), and up to four gamepads.

## Accessing Input Devices

```csharp
// From inside an Entity method (CustomInitialize or CustomActivity):
var keyboard = Engine.InputManager.Keyboard;       // IKeyboard
var cursor   = Engine.InputManager.Cursor;         // ICursor
var gamepad0 = Engine.InputManager.GetGamepad(0);  // IGamepad, index 0–3
```

`GetGamepad(int index)` throws `ArgumentOutOfRangeException` for index values outside 0–3.

## IKeyboard

```csharp
IKeyboard kb = Engine.InputManager.Keyboard;

bool held       = kb.IsKeyDown(Keys.Space);         // true every frame while held
bool pressed    = kb.WasKeyPressed(Keys.Space);     // true only on first frame down
bool released   = kb.WasKeyJustReleased(Keys.Space);// true only on first frame up
```

## ICursor (Mouse / Touch)

```csharp
ICursor cursor = Engine.InputManager.Cursor;

bool clicking       = cursor.PrimaryDown;           // left button held
bool justClicked    = cursor.PrimaryPressed;        // left button just pressed
Vector2 worldPos    = cursor.WorldPosition;         // position in world space (Y+ up)
Vector2 screenPos   = cursor.ScreenPosition;        // position in screen pixels
```

## IGamepad

```csharp
IGamepad pad = Engine.InputManager.GetGamepad(0);

bool held       = pad.IsButtonDown(Buttons.A);
bool justPressed  = pad.WasButtonJustPressed(Buttons.A);
bool justReleased = pad.WasButtonJustReleased(Buttons.A);

float axisValue = pad.GetAxis(GamepadAxis.LeftStickX);  // -1.0 to 1.0
```

## KeyboardInput2D — Directional Movement from Four Keys

`KeyboardInput2D` implements `I2DInput` and maps four keys to a normalized X/Y pair.

```csharp
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;

public class Player : Entity
{
    private KeyboardInput2D _movement = null!;

    public override void CustomInitialize()
    {
        _movement = new KeyboardInput2D(
            Engine.InputManager.Keyboard,
            Keys.Left,   // left  → X = -1
            Keys.Right,  // right → X = +1
            Keys.Up,     // up    → Y = +1
            Keys.Down);  // down  → Y = -1
    }

    public override void CustomActivity(FrameTime time)
    {
        const float Speed = 200f;
        VelocityX = _movement.X * Speed;
        VelocityY = _movement.Y * Speed;
    }
}
```

`_movement.X` and `_movement.Y` return `-1`, `0`, or `1`. Y+ is **up** (matching world-space coordinates).

## KeyboardPressableInput — Single Key as IPressableInput

Wraps one key as a standard `IPressableInput` (useful when passing input to a system that expects a button).

```csharp
using FlatRedBall2.Input;

var jumpKey = new KeyboardPressableInput(Engine.InputManager.Keyboard, Keys.Space);

bool held      = jumpKey.IsDown;
bool pressed   = jumpKey.WasJustPressed;
bool released  = jumpKey.WasJustReleased;
```

## GamepadInput2D — Directional Movement from Two Axes

Maps two `GamepadAxis` values to `I2DInput`:

```csharp
var stick = new GamepadInput2D(
    Engine.InputManager.GetGamepad(0),
    GamepadAxis.LeftStickX,
    GamepadAxis.LeftStickY);

VelocityX = stick.X * Speed;
VelocityY = stick.Y * Speed;
```

## GamepadPressableInput — Single Button as IPressableInput

```csharp
var jumpButton = new GamepadPressableInput(
    Engine.InputManager.GetGamepad(0),
    Buttons.A);

if (jumpButton.WasJustPressed) { /* jump */ }
```

## Best Practices

- **Create input objects once in `CustomInitialize`, not in `CustomActivity`.** Constructing them every frame allocates garbage and is wasteful.
- **`Engine` is not available in the constructor** — see `engine-overview` Key Design Rules. Always initialize input in `CustomInitialize`.
- **Y+ is up** — `KeyboardInput2D` already accounts for this. Up key → `Y = +1`.
- **Check `ICursor.WorldPosition` for click-to-move logic** — it returns coordinates in the same world space as entity positions.

## Common Pitfalls

- **Input feels one frame late** — Input state is captured once at the start of each frame, before any entity logic runs. This is by design.
- **Gamepad index out of range** — `GetGamepad` only accepts 0–3. Wrap in a try/catch or validate the index if it comes from user data.
- **`WasKeyPressed` fires repeatedly** — This means you're checking `IsKeyDown` instead of `WasKeyPressed`. Use `WasKeyPressed` for one-shot actions.
