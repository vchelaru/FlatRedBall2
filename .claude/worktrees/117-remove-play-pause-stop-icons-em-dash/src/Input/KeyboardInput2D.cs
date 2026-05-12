using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts four keyboard <see cref="Keys"/> to an <see cref="I2DInput"/>, suitable for arrow-key or
/// WASD movement. Each axis returns one of <c>-1</c>, <c>0</c>, or <c>+1</c> based on which keys
/// are held; opposite keys held simultaneously cancel to <c>0</c>.
/// </summary>
/// <remarks>
/// Y+ is up: holding the <c>up</c> key reports <c>Y = +1</c>, matching world-space coordinates.
/// The result is not normalized to unit length — diagonal input has magnitude √2. Multiply by your
/// movement speed and (if needed) normalize before applying.
/// </remarks>
public class KeyboardInput2D : I2DInput
{
    private readonly IKeyboard _keyboard;
    private readonly Keys _left, _right, _up, _down;

    /// <summary>
    /// Creates a 2D input from four directional keys.
    /// </summary>
    /// <param name="keyboard">The source keyboard. Obtain via <c>Engine.Input.Keyboard</c>.</param>
    /// <param name="left">Held → <see cref="X"/> contributes <c>-1</c>.</param>
    /// <param name="right">Held → <see cref="X"/> contributes <c>+1</c>.</param>
    /// <param name="up">Held → <see cref="Y"/> contributes <c>+1</c> (Y+ up).</param>
    /// <param name="down">Held → <see cref="Y"/> contributes <c>-1</c>.</param>
    public KeyboardInput2D(IKeyboard keyboard, Keys left, Keys right, Keys up, Keys down)
    {
        _keyboard = keyboard;
        _left = left;
        _right = right;
        _up = up;
        _down = down;
    }

    /// <summary>−1 if only the left key is held, +1 if only the right key is held, 0 otherwise (including both held).</summary>
    public float X
    {
        get
        {
            var x = 0f;
            if (_keyboard.IsKeyDown(_left))  x -= 1f;
            if (_keyboard.IsKeyDown(_right)) x += 1f;
            return x;
        }
    }

    /// <summary>+1 if only the up key is held (Y+ up), −1 if only the down key is held, 0 otherwise (including both held).</summary>
    public float Y
    {
        get
        {
            var y = 0f;
            if (_keyboard.IsKeyDown(_down)) y -= 1f;
            if (_keyboard.IsKeyDown(_up))   y += 1f;
            return y;
        }
    }
}
