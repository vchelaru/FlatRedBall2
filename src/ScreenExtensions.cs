using System;

namespace FlatRedBall2;

public static class ScreenExtensions
{
    /// <summary>
    /// Restarts the current screen using <paramref name="newConfigure"/> instead of the previously
    /// retained callback. The new callback fully replaces the retained one — both for this restart
    /// and for any future <c>RestartScreen()</c> call that doesn't supply its own. Extension form
    /// lets <typeparamref name="T"/> be inferred from the receiver, so callers write
    /// <c>playerScreen.RestartScreen(s =&gt; s.LevelIndex++)</c> without explicit type args.
    /// </summary>
    public static void RestartScreen<T>(this T screen, Action<T> newConfigure, RestartMode mode = RestartMode.DeathRetry)
        where T : Screen
        => screen.Engine.RequestScreenRestart(s => newConfigure((T)s), mode);
}
