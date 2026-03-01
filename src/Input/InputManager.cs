using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

public class InputManager
{
    private readonly Keyboard _keyboard = new Keyboard();
    private readonly Cursor _cursor = new Cursor();
    private readonly Gamepad[] _gamepads;

    public InputManager()
    {
        _gamepads = new Gamepad[4];
        for (int i = 0; i < 4; i++)
            _gamepads[i] = new Gamepad(i);
    }

    public IKeyboard Keyboard => _keyboard;
    public ICursor Cursor => _cursor;

    public IGamepad GetGamepad(int index) => _gamepads[index];

    internal void SetCamera(Camera camera) => _cursor.SetCamera(camera);

    internal void Update()
    {
        _keyboard.Update();
        _cursor.Update();
        foreach (var gp in _gamepads)
            gp.Update();
    }
}
