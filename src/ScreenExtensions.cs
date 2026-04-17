using System;

namespace FlatRedBall2;

public static class ScreenExtensions
{
    /// <summary>
    /// Restarts the current screen, replaying the original configure callback and then applying
    /// <paramref name="extraConfigure"/> on top. Extension form lets <typeparamref name="T"/> be
    /// inferred from the receiver, so callers write
    /// <c>playerScreen.RestartScreen(s =&gt; s.LevelIndex++)</c> without explicit type args.
    /// <para>
    /// The extra configure is one-shot: it does not persist across subsequent restarts.
    /// </para>
    /// </summary>
    public static void RestartScreen<T>(this T screen, Action<T> extraConfigure) where T : Screen
        => screen.Engine.RequestScreenRestart(s => extraConfigure((T)s));
}
