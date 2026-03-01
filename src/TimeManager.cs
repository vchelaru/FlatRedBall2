using System;
using Microsoft.Xna.Framework;

namespace FlatRedBall2;

public class TimeManager
{
    private TimeSpan _sinceGameStart;
    private TimeSpan _sinceScreenStart;

    public float TimeScale { get; set; } = 1f;
    public FrameTime CurrentFrameTime { get; private set; }

    public void ResetScreen() => _sinceScreenStart = TimeSpan.Zero;

    internal void Update(GameTime gameTime)
    {
        var scaledDelta = TimeSpan.FromSeconds(gameTime.ElapsedGameTime.TotalSeconds * TimeScale);
        _sinceGameStart += scaledDelta;
        _sinceScreenStart += scaledDelta;
        CurrentFrameTime = new FrameTime(scaledDelta, _sinceScreenStart, _sinceGameStart);
    }
}
