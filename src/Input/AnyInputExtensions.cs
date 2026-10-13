using System;
using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// "Press any button" helpers for <see cref="IGamepad"/>, for title screens, join prompts, and rebinding.
/// To wait for one without polling in <c>CustomActivity</c>:
/// <c>await Engine.Time.DelayUntil(() =&gt; pad.WasAnyButtonJustPressed())</c>.
/// </summary>
public static class GamepadAnyButtonExtensions
{
    private static readonly Buttons[] Digital =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.Start, Buttons.Back, Buttons.BigButton,
        Buttons.LeftStick, Buttons.RightStick,
    };

    private static readonly Buttons[] DPad =
    {
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    };

    private static readonly Buttons[] Triggers = { Buttons.LeftTrigger, Buttons.RightTrigger };

    private static readonly Buttons[] ThumbstickDirections =
    {
        Buttons.LeftThumbstickUp, Buttons.LeftThumbstickDown, Buttons.LeftThumbstickLeft, Buttons.LeftThumbstickRight,
        Buttons.RightThumbstickUp, Buttons.RightThumbstickDown, Buttons.RightThumbstickLeft, Buttons.RightThumbstickRight,
    };

    /// <summary>True if any button went from up to down this frame. See <see cref="GetButtonJustPressed"/> for the options.</summary>
    public static bool WasAnyButtonJustPressed(this IGamepad gamepad, bool ignoreDirectionals = false, bool ignoreAnalogs = false) =>
        gamepad.GetButtonJustPressed(ignoreDirectionals, ignoreAnalogs) != null;

    /// <summary>
    /// Returns a button that went from up to down this frame, or null if none did. If several went down on the same
    /// frame, face/shoulder/menu buttons win over the d-pad, then triggers, then stick directions.
    /// </summary>
    /// <param name="gamepad">The gamepad to check.</param>
    /// <param name="ignoreDirectionals">Skip the d-pad and the stick-direction buttons (<c>Buttons.LeftThumbstickLeft</c> etc.), so a bumped stick or d-pad does not count.</param>
    /// <param name="ignoreAnalogs">Skip the triggers and the stick-direction buttons, so a resting trigger or drifting stick does not count.</param>
    public static Buttons? GetButtonJustPressed(this IGamepad gamepad, bool ignoreDirectionals = false, bool ignoreAnalogs = false)
    {
        var found = FirstJustPressed(gamepad, Digital);
        if (found != null) return found;

        if (!ignoreDirectionals)
        {
            found = FirstJustPressed(gamepad, DPad);
            if (found != null) return found;
        }

        if (!ignoreAnalogs)
        {
            found = FirstJustPressed(gamepad, Triggers);
            if (found != null) return found;
        }

        if (!ignoreDirectionals && !ignoreAnalogs)
            return FirstJustPressed(gamepad, ThumbstickDirections);

        return null;
    }

    private static Buttons? FirstJustPressed(IGamepad gamepad, Buttons[] buttons)
    {
        foreach (var button in buttons)
            if (gamepad.WasButtonJustPressed(button))
                return button;
        return null;
    }
}

/// <summary>"Press any key" helpers for <see cref="IKeyboard"/>, for title screens and rebinding.</summary>
public static class KeyboardAnyKeyExtensions
{
    private static readonly Keys[] AllKeys = Array.FindAll(Enum.GetValues<Keys>(), k => k != Keys.None);

    /// <summary>True if any key went from up to down this frame.</summary>
    public static bool WasAnyKeyPressed(this IKeyboard keyboard) => keyboard.GetKeyPressed() != null;

    /// <summary>
    /// Returns a key that went from up to down this frame, or null if none did. If several went down on the same
    /// frame, the one with the lowest <see cref="Keys"/> value wins.
    /// </summary>
    public static Keys? GetKeyPressed(this IKeyboard keyboard)
    {
        foreach (var key in AllKeys)
            if (keyboard.WasKeyPressed(key))
                return key;
        return null;
    }
}
