using System;

namespace FlatRedBall2;

public readonly record struct FrameTime(TimeSpan Delta, TimeSpan SinceScreenStart, TimeSpan SinceGameStart)
{
    public float DeltaSeconds => (float)Delta.TotalSeconds;
}
