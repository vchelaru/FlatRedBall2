// SCRATCH (issue #1528 measurement). Never committed.
using System;
using System.Diagnostics;

namespace FlatRedBall2;

public static class ProbeFlags
{
    public static string Query = "";
    public static Action? AfterScreen;
    public static bool Has(string f) => Query.Contains(f, StringComparison.OrdinalIgnoreCase);
    public static string? Get(string key)
    {
        foreach (var part in Query.TrimStart('?').Split('&'))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase)) return kv[1];
        }
        return null;
    }
    public static void Log(string s) => Console.WriteLine("[probe] " + s);
    public static long Now() => Stopwatch.GetTimestamp();
    public static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
}

public sealed class ProbeFontCreator : RenderingLibrary.Graphics.Fonts.IInMemoryFontCreator,
    RenderingLibrary.Graphics.Fonts.IGrowableFontCreator
{
    private readonly KernSmith.Gum.KernSmithFontCreator _inner;
    public ProbeFontCreator(KernSmith.Gum.KernSmithFontCreator inner) { _inner = inner; }
    public RenderingLibrary.Graphics.BitmapFont? TryCreateFont(RenderingLibrary.Graphics.Fonts.BmfcSave b)
    {
        var t = ProbeFlags.Now();
        var f = _inner.TryCreateFont(b);
        ProbeFlags.Log($"font create name={b.FontName} size={b.FontSize} bold={b.IsBold} italic={b.IsItalic} outline={b.OutlineThickness} ranges={b.Ranges} => {ProbeFlags.Ms(t):F1}ms");
        return f;
    }
    public System.Collections.Generic.IReadOnlyList<char>? TryAddGlyphs(RenderingLibrary.Graphics.BitmapFont font, RenderingLibrary.Graphics.Fonts.BmfcSave b, string characters)
    {
        var t = ProbeFlags.Now();
        var r = ((RenderingLibrary.Graphics.Fonts.IGrowableFontCreator)_inner).TryAddGlyphs(font, b, characters);
        ProbeFlags.Log($"font addglyphs size={b.FontSize} chars={characters.Length} => {ProbeFlags.Ms(t):F1}ms");
        return r;
    }
}
