namespace FlatRedBall2.Input;

/// <summary>How <see cref="IGamepad.Deadzone"/> is measured on the thumbsticks.</summary>
public enum GamepadDeadzoneType
{
    /// <summary>Each axis is zeroed on its own, so a barely-moved Y reads 0 even while X is pushed hard. MonoGame's <c>IndependentAxes</c>.</summary>
    PerAxis,

    /// <summary>The stick is zeroed when its distance from center is within the deadzone; direction is preserved outside it. MonoGame's <c>Circular</c>. Better for free-aim.</summary>
    Radial,
}
