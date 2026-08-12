using FlatRedBall2.Glue.Model;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Glue;

/// <summary>
/// How an entity is driven, as recorded by Glue's entity input/movement plugin.
/// </summary>
/// <remarks>
/// Ordinals are Glue's own (<c>EntityInputMovementPlugin</c>'s <c>MainViewModel.InputDevice</c>) and
/// are the on-disk format. Note the absent default is <see cref="GamepadWithKeyboardFallback"/>, not
/// <see cref="None"/> — an entity whose file says nothing about input still expects to be driven.
/// </remarks>
public enum GlueInputDevice
{
    /// <summary>Gamepad 0 when it is connected, otherwise the keyboard.</summary>
    GamepadWithKeyboardFallback = 0,

    /// <summary>Nothing is bound; the game wires its own input.</summary>
    None = 1,

    /// <summary>A real input that always reads zero — a deliberately motionless entity.</summary>
    ZeroInputDevice = 2,
}

/// <summary>The inputs an entity is driven by, or nulls when the author wired their own.</summary>
/// <param name="MovementInput">Directional movement, for both platformer and top-down.</param>
/// <param name="JumpInput">The jump/primary action, used only by a platformer.</param>
public readonly record struct GlueBoundInput(I2DInput? MovementInput, IPressableInput? JumpInput);

/// <summary>
/// Turns an entity's <c>InputDevice</c> choice into the inputs a movement behaviour reads.
/// </summary>
/// <remarks>
/// This is the one place the loader decides a control scheme, which is why it mirrors Glue's own
/// generated code rather than inventing one: gamepad 0 when connected, keyboard otherwise, with the
/// left stick and WASD (plus arrow keys) for movement and A / Space to jump.
/// </remarks>
public static class GlueInputBinder
{
    /// <summary>Binds the inputs <paramref name="save"/> asks for.</summary>
    public static GlueBoundInput Bind(EntitySave save, InputManager input)
    {
        return (GlueInputDevice)save.InputDevice switch
        {
            GlueInputDevice.None => default,

            GlueInputDevice.ZeroInputDevice =>
                new GlueBoundInput(I2DInput.Zero, IPressableInput.Zero),

            _ => BindGamepadOrKeyboard(input),
        };
    }

    /// <summary>
    /// Both devices at once, rather than Glue's pick-one-at-creation.
    /// </summary>
    /// <remarks>
    /// Glue's generated <c>InitializeInput</c> tests <c>Xbox360GamePads[0].IsConnected</c> once and
    /// binds one device for the entity's life. FRB2 has no connection concept — a disconnected
    /// gamepad reads zero — so combining the two reproduces the intended behaviour and improves on
    /// it: a controller plugged in mid-game takes over without recreating the entity.
    /// </remarks>
    private static GlueBoundInput BindGamepadOrKeyboard(InputManager input)
    {
        var gamepad = input.GetGamepad(0);

        // WASD is what FRB1 binds: its generated InitializeInput uses Keyboard.Default2DInput, which
        // is WASD and nothing else. Arrows are added on top — FRB1 gives them no default meaning, so
        // accepting both keeps parity and takes nothing away from a loaded project.
        var movement = new GamepadInput2D(gamepad, GamepadAxis.LeftStickX, GamepadAxis.LeftStickY)
            .Or(new KeyboardInput2D(input.Keyboard, Keys.A, Keys.D, Keys.W, Keys.S))
            .Or(new KeyboardInput2D(input.Keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down));

        var jump = new AnyPressableInput(
            new GamepadPressableInput(gamepad, Buttons.A),
            new KeyboardPressableInput(input.Keyboard, Keys.Space));

        return new GlueBoundInput(movement, jump);
    }
}
