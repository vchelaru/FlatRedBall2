using System;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// What a color or alpha field asks of each selected frame's channel: leave it, clear it (unset, so
/// it inherits), or apply a <see cref="NumericEdit"/>. A plain <c>int?</c> can't work here because a
/// <c>null</c> channel is a real value ("unset"), so it can't also mean "leave alone" for a
/// "(mixed)" field the user never typed in (#1330).
/// </summary>
public readonly struct ChannelEdit
{
    private readonly NumericEdit? _edit;
    private readonly bool _clear;

    private ChannelEdit(NumericEdit? edit, bool clear)
    {
        _edit = edit;
        _clear = clear;
    }

    public static ChannelEdit Keep => default;
    public static ChannelEdit Clear => new(null, clear: true);
    public static ChannelEdit Edit(NumericEdit edit) => new(edit, clear: false);

    /// <summary>A value sets the channel; <c>null</c> clears it.</summary>
    public static implicit operator ChannelEdit(int? value) => value is int v ? Edit(v) : Clear;

    /// <summary>The channel's new value. A relative edit starts from <paramref name="current"/>, or
    /// from <paramref name="inherited"/> when the channel is unset, and the result is rounded and
    /// clamped to <paramref name="min"/>..<paramref name="max"/>.</summary>
    public int? Apply(int? current, int inherited, int min, int max)
    {
        if (_clear) return null;
        if (_edit is not NumericEdit edit) return current;
        decimal result = Math.Clamp(edit.Apply((decimal)(current ?? inherited)), min, max);
        return (int)Math.Round(result, MidpointRounding.AwayFromZero);
    }
}
