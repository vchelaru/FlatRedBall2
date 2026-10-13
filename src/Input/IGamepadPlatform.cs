using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>Seam over MonoGame's static <c>GamePad</c> so <see cref="Gamepad"/> can run headless in tests.</summary>
internal interface IGamepadPlatform
{
    GamePadState GetState(int index, GamePadDeadZone deadZone);
    bool SetVibration(int index, float leftMotor, float rightMotor);
}

internal sealed class MonoGameGamepadPlatform : IGamepadPlatform
{
    public GamePadState GetState(int index, GamePadDeadZone deadZone) => GamePad.GetState(index, deadZone);

    public bool SetVibration(int index, float leftMotor, float rightMotor) => GamePad.SetVibration(index, leftMotor, rightMotor);
}
