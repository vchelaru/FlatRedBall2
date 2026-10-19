using System;
using System.Collections.Generic;

namespace Solitaire.Entities;

/// <summary>
/// Remembers the last value pushed to an expensive-to-apply target (a Gum state) so
/// repeating the same value is skipped. Call <see cref="Invalidate"/> whenever the target
/// can change without going through <see cref="Apply"/>.
/// </summary>
public sealed class AppliedStateCache<T>
{
    private bool _hasValue;
    private T? _value;

    public void Apply(T value, Action<T> apply)
    {
        if (_hasValue && EqualityComparer<T>.Default.Equals(_value, value)) return;
        apply(value);
        _value = value;
        _hasValue = true;
    }

    public void Invalidate() => _hasValue = false;
}
