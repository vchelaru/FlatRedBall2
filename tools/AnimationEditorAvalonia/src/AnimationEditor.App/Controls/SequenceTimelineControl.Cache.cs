using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AnimationEditor.App.Controls;

// What a frame would otherwise rebuild on every scroll step: the theme brushes and the laid-out text. Both depend only on
// the theme, the text and (for a trimmed label) the width it is given, never on the scroll offset, so a scroll reuses them.
public sealed partial class SequenceTimelineControl
{
    // Past this many laid-out texts the cache starts over, so a long session of zooming can't grow it without bound.
    private const int MaxCachedTexts = 2048;

    private readonly record struct TextKey(string Text, double Size, IBrush Brush, FontWeight Weight, double MaxWidth, bool Mono);

    private readonly Dictionary<string, IBrush> _brushes = new();
    private readonly Dictionary<TextKey, FormattedText> _texts = new();

    // The cached brushes and texts belong to a theme variant; a new variant resolves the tokens again.
    private void ClearThemeCaches()
    {
        _brushes.Clear();
        _texts.Clear();
    }

    // The theme tokens live in ThemeDictionaries, which a lookup without the theme variant never searches.
    private IBrush Brush(string key)
    {
        if (_brushes.TryGetValue(key, out var cached)) return cached;
        if (!this.TryFindResource(key, ActualThemeVariant, out var value)) return Brushes.Gray;
        var brush = value switch
        {
            IBrush found => found,
            Color color => new SolidColorBrush(color),
            _ => Brushes.Gray,
        };
        _brushes[key] = brush;
        return brush;
    }

    /// <summary>A single line of text; <paramref name="mono"/> uses the mono font, for a number that changes while it is on screen. The result is shared: draw it, don't change it.</summary>
    private FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal, bool mono = false) =>
        CachedText(text, size, brush, weight, double.PositiveInfinity, mono);

    /// <summary>One line trimmed with an ellipsis to <paramref name="maxWidth"/>. The result is shared: draw it, don't change it.</summary>
    private FormattedText TrimmedText(string text, double size, IBrush brush, FontWeight weight, double maxWidth) =>
        CachedText(text, size, brush, weight, maxWidth, mono: false);

    private FormattedText CachedText(string text, double size, IBrush brush, FontWeight weight, double maxWidth, bool mono)
    {
        Stats.Texts++;
        var key = new TextKey(text, size, brush, weight, maxWidth, mono);
        if (_texts.TryGetValue(key, out var cached)) return cached;
        Stats.TextsCreated++;
        var created = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(mono && this.TryFindResource("MonoFont", ActualThemeVariant, out var font) && font is FontFamily family ? family : FontFamily.Default, FontStyle.Normal, weight), size, brush);
        if (!double.IsPositiveInfinity(maxWidth))
        {
            created.MaxTextWidth = maxWidth;
            created.MaxLineCount = 1;
            created.Trimming = TextTrimming.CharacterEllipsis;
        }
        if (_texts.Count >= MaxCachedTexts) _texts.Clear();
        _texts[key] = created;
        return created;
    }
}
