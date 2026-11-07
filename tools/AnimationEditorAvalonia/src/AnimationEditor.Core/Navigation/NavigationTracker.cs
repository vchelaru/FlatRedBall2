using System;

namespace AnimationEditor.Core.Navigation;

/// <summary>
/// Turns user actions into <see cref="NavigationHistory"/> entries. The host wraps every deliberate action that can
/// move the user (a click, a key press, an open, a tab close) in <see cref="BeginUserAction"/>; when the outermost
/// action ends, a changed location pushes the one the action started from, subject to
/// <see cref="NavigationCoalescing"/>. Anything outside an action (undo, load, rebuilds, playback) records nothing,
/// and <see cref="Suppress"/> additionally cancels any action already open, which is how Back and Forward restore
/// a place without recording it.
/// </summary>
public sealed class NavigationTracker
{
    private readonly NavigationHistory _history;
    private readonly Func<NavigationLocation?> _capture;
    private readonly Func<NavigationLocation, bool> _isStillPresent;
    private readonly Func<TimeSpan> _now;

    private int _actionDepth;
    private int _suppressDepth;
    private bool _cancelled;
    private NavigationLocation? _before;
    private TimeSpan? _lastChangeAt;

    /// <param name="capture">Reads the current place fresh; null when no tab is open.</param>
    /// <param name="isStillPresent">
    /// Whether a place the user just left still exists (a renamed or deleted chain does not), so dead places are not
    /// recorded.
    /// </param>
    /// <param name="now">A monotonic clock, injectable for tests.</param>
    public NavigationTracker(
        NavigationHistory history,
        Func<NavigationLocation?> capture,
        Func<NavigationLocation, bool> isStillPresent,
        Func<TimeSpan> now)
    {
        _history = history;
        _capture = capture;
        _isStillPresent = isStillPresent;
        _now = now;
    }

    /// <summary>
    /// Opens a user action. Actions nest and may overlap in any order; the place is read when the first one opens
    /// and compared when the last one is disposed.
    /// </summary>
    public IDisposable BeginUserAction()
    {
        if (_actionDepth++ == 0)
        {
            _before = _capture();
            _cancelled = false;
        }
        return new Scope(EndUserAction);
    }

    /// <summary>
    /// Records nothing while any suppression is open, and cancels the user action in progress. Ending the last
    /// suppression starts the next frame move as a fresh run.
    /// </summary>
    public IDisposable Suppress()
    {
        _suppressDepth++;
        _cancelled = true;
        return new Scope(EndSuppress);
    }

    private void EndSuppress()
    {
        if (--_suppressDepth == 0)
            _lastChangeAt = null;
    }

    private void EndUserAction()
    {
        if (--_actionDepth > 0) return;
        var before = _before;
        _before = null;
        if (_cancelled || _suppressDepth > 0 || before is not { } leaving) return;

        var arriving = _capture();
        if (arriving == leaving) return;

        var now = _now();
        TimeSpan? since = _lastChangeAt is { } last ? now - last : null;
        _lastChangeAt = now;
        if ((arriving is not { } to || NavigationCoalescing.ShouldRecord(leaving, to, since)) && _isStillPresent(leaving))
            _history.Push(leaving);
    }

    private sealed class Scope : IDisposable
    {
        private Action? _end;
        public Scope(Action end) => _end = end;

        public void Dispose()
        {
            _end?.Invoke();
            _end = null;
        }
    }
}
