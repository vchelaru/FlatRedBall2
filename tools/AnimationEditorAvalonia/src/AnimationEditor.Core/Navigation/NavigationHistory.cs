using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Navigation;

/// <summary>
/// Browser-style back and forward over <see cref="NavigationLocation"/>s. The current location is not stored here:
/// the caller reads it fresh (selection changes without any push) and hands it in when it moves.
/// </summary>
public sealed class NavigationHistory
{
    /// <summary>The most places remembered in each direction; the oldest is dropped past this.</summary>
    public const int DefaultCapacity = 50;

    private readonly int _capacity;
    private readonly List<NavigationLocation> _back = new();
    private readonly List<NavigationLocation> _forward = new();

    public NavigationHistory(int capacity = DefaultCapacity) => _capacity = capacity;

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    /// <summary>
    /// Records <paramref name="leaving"/>, the place the user is about to navigate away from on purpose, and drops
    /// the forward list. A repeat of the newest entry is ignored.
    /// </summary>
    public void Push(NavigationLocation leaving)
    {
        _forward.Clear();
        if (_back.Count > 0 && _back[^1] == leaving) return;
        _back.Add(leaving);
        Trim(_back);
    }

    /// <summary>
    /// Pops the newest back entry that <paramref name="isValid"/> accepts and returns it, remembering
    /// <paramref name="current"/> for Forward. Invalid entries passed over are discarded. Returns null when no
    /// entry is valid.
    /// </summary>
    public NavigationLocation? Back(NavigationLocation current, Func<NavigationLocation, bool> isValid) =>
        Step(_back, _forward, current, isValid);

    /// <summary>The mirror of <see cref="Back"/>.</summary>
    public NavigationLocation? Forward(NavigationLocation current, Func<NavigationLocation, bool> isValid) =>
        Step(_forward, _back, current, isValid);

    private NavigationLocation? Step(
        List<NavigationLocation> from, List<NavigationLocation> to, NavigationLocation current, Func<NavigationLocation, bool> isValid)
    {
        while (from.Count > 0)
        {
            var candidate = from[^1];
            from.RemoveAt(from.Count - 1);
            if (!isValid(candidate)) continue;
            to.Add(current);
            Trim(to);
            return candidate;
        }
        return null;
    }

    private void Trim(List<NavigationLocation> list)
    {
        if (list.Count > _capacity)
            list.RemoveRange(0, list.Count - _capacity);
    }
}
