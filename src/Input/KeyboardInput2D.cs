using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public class KeyboardInput2D : I2DInput
{
    private readonly IKeyboard _keyboard;
    private readonly Keys _left, _right, _up, _down;

    public KeyboardInput2D(IKeyboard keyboard, Keys left, Keys right, Keys up, Keys down)
    {
        _keyboard = keyboard;
        _left = left;
        _right = right;
        _up = up;
        _down = down;
    }

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
