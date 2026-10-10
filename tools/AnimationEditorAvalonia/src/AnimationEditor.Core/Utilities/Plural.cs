namespace AnimationEditor.Core.Utilities;

/// <summary>
/// Count-aware noun forms, so UI text says "1 frame" / "3 frames" instead of hedging with "frame(s)".
/// Route every count-bearing string through here.
/// </summary>
public static class Plural
{
    /// <summary>The singular form for exactly 1, otherwise <paramref name="plural"/> (default: singular + "s").</summary>
    public static string Noun(long count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";

    /// <summary>"<c>3 frames</c>": the count followed by its <see cref="Noun"/>.</summary>
    public static string Format(long count, string singular, string? plural = null) =>
        $"{count} {Noun(count, singular, plural)}";
}
