using System;
using System.Globalization;
using System.Text;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Runtime opt-in perf probe for builds where rebuilding to measure is expensive (a Release AOT
/// browser publish). A host creates one from the page's query string with
/// <see cref="TryCreateFromQuery"/>: with <c>?diag=perf</c> (optionally <c>&amp;frames=N</c>,
/// default 300) it enables <see cref="FlatRedBallService.Performance"/> and
/// <see cref="FlatRedBallService.RenderDiagnostics"/>, collects N frames, then writes ONE
/// <c>[frb-diag] {json}</c> line through the supplied writer and switches them back off. Without
/// the param the factory returns <c>null</c> and nothing is enabled.
/// </summary>
/// <remarks>
/// The writer should be <c>Console.WriteLine</c>, not <c>Debug.WriteLine</c>, which is stripped
/// from Release/AOT builds. Call <see cref="OnTick"/> once per host tick (e.g. after
/// <c>Game.Tick()</c>). Samples are held in one array allocated up front, so collecting adds no
/// per-frame allocation, and the single write happens after the last sampled frame, outside every
/// timed phase.
/// </remarks>
public sealed class PerfDiagnosticsProbe
{
    /// <summary>Stable prefix of the console line, for scripts that grep or filter console output.</summary>
    public const string LinePrefix = "[frb-diag] ";

    internal const int DefaultFrames = 300;

    // Bounds the up-front sample allocation (about 5 MB) against a hostile or mistyped query.
    internal const int MaxFrames = 100_000;

    private struct Sample
    {
        public double DrawMs, UpdateMs, FrameMs;
        public long DrawCalls, Sprites, Primitives, Textures;
        public int BatchBreaks;
    }

    private readonly FlatRedBallService _service;
    private readonly Action<string> _writeLine;
    private readonly Action<string>? _publishJson;
    private readonly Sample[] _samples;
    private readonly bool _performanceWasEnabled;
    private readonly bool _renderDiagnosticsWasEnabled;
    private int _count;
    private long _lastFramesRecorded;
    private bool _done;

    private PerfDiagnosticsProbe(FlatRedBallService service, int frames, string? platformLabel,
        Action<string> writeLine, Action<string>? publishJson)
    {
        _service = service;
        _writeLine = writeLine;
        _publishJson = publishJson;
        _samples = new Sample[frames];

        _performanceWasEnabled = service.Performance.IsEnabled;
        _renderDiagnosticsWasEnabled = service.RenderDiagnostics.IsEnabled;
        service.Performance.IsEnabled = true;
        service.RenderDiagnostics.IsEnabled = true;
        _lastFramesRecorded = service.Performance.FramesRecorded;
        if (platformLabel != null)
            service.Performance.PlatformLabel = platformLabel;
    }

    /// <summary>
    /// Returns a probe when <paramref name="query"/> (a URL query string, with or without the
    /// leading <c>?</c>) contains <c>diag=perf</c>, enabling the diagnostics it reads; otherwise
    /// <c>null</c> and nothing is touched. <paramref name="getPlatformLabel"/> supplies the browser
    /// name (the engine cannot read <c>navigator.userAgent</c> itself) and is only called when the
    /// probe is actually enabled, so a host's JS interop costs nothing otherwise. <paramref name="writeLine"/>
    /// receives the full prefixed line. <paramref name="publishJson"/>, if given, receives the bare
    /// JSON object so the host can hand it to JavaScript.
    /// </summary>
    public static PerfDiagnosticsProbe? TryCreateFromQuery(string? query, FlatRedBallService service,
        Func<string?>? getPlatformLabel, Action<string> writeLine, Action<string>? publishJson = null)
    {
        if (!TryParseQuery(query, out var frames))
            return null;
        return new PerfDiagnosticsProbe(service, frames, getPlatformLabel?.Invoke(), writeLine, publishJson);
    }

    /// <summary>
    /// Call once per host tick. Counts a frame only when the engine actually completed one since
    /// the previous call (a tick that skipped Draw does not count), and writes the report when the
    /// requested number of frames has been collected. No-op after that.
    /// </summary>
    public void OnTick()
    {
        if (_done) return;

        var performance = _service.Performance;
        var recorded = performance.FramesRecorded;
        if (recorded == _lastFramesRecorded) return;
        _lastFramesRecorded = recorded;

        var profile = performance.LastProfile;
        _samples[_count++] = new Sample
        {
            DrawMs = profile.DrawTotalMs,
            UpdateMs = profile.UpdateTotalMs,
            FrameMs = profile.FrameTotalMs,
            DrawCalls = profile.DrawCallCount,
            Sprites = profile.SpriteCount,
            Primitives = profile.PrimitiveCount,
            Textures = profile.TextureCount,
            BatchBreaks = _service.RenderDiagnostics.BatchBreakCount,
        };

        if (_count < _samples.Length) return;

        _done = true;
        var json = FormatJson(_samples, performance.PlatformLabel, performance.TimerResolutionMs);
        _service.Performance.IsEnabled = _performanceWasEnabled;
        _service.RenderDiagnostics.IsEnabled = _renderDiagnosticsWasEnabled;
        _writeLine(LinePrefix + json);
        _publishJson?.Invoke(json);
    }

    // Accepts "?diag=perf&frames=120" with or without the leading '?'. Anything other than
    // diag=perf is "not requested". A missing, non-numeric, or non-positive frames value falls
    // back to the default rather than disabling the probe: the user clearly asked for it.
    internal static bool TryParseQuery(string? query, out int frames)
    {
        frames = DefaultFrames;
        if (string.IsNullOrEmpty(query)) return false;

        var diagPerf = false;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = Uri.UnescapeDataString(pair.Substring(0, eq));
            var value = Uri.UnescapeDataString(pair.Substring(eq + 1));

            if (key.Equals("diag", StringComparison.OrdinalIgnoreCase))
                diagPerf = value.Equals("perf", StringComparison.OrdinalIgnoreCase);
            else if (key.Equals("frames", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
                frames = System.Math.Min(n, MaxFrames);
        }

        if (!diagPerf) frames = DefaultFrames;
        return diagPerf;
    }

    private static string FormatJson(Sample[] samples, string? platform, double timerResolutionMs)
    {
        var sb = new StringBuilder(512);
        sb.Append("{\"platform\":");
        AppendJsonString(sb, platform ?? "unknown");
        sb.Append(",\"frames\":").Append(samples.Length.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"timerResolutionMs\":").Append(Num(timerResolutionMs));
        AppendStat(sb, "drawMs", samples, static s => s.DrawMs);
        AppendStat(sb, "updateMs", samples, static s => s.UpdateMs);
        AppendStat(sb, "frameMs", samples, static s => s.FrameMs);
        AppendStat(sb, "drawCalls", samples, static s => s.DrawCalls);
        AppendStat(sb, "sprites", samples, static s => s.Sprites);
        AppendStat(sb, "primitives", samples, static s => s.Primitives);
        AppendStat(sb, "textures", samples, static s => s.Textures);
        AppendStat(sb, "batchBreaks", samples, static s => s.BatchBreaks);
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendStat(StringBuilder sb, string name, Sample[] samples, Func<Sample, double> selector)
    {
        var values = new double[samples.Length];
        for (int i = 0; i < values.Length; i++) values[i] = selector(samples[i]);
        Array.Sort(values);

        sb.Append(",\"").Append(name).Append("\":{\"median\":").Append(Num(NearestRank(values, 0.5)));
        sb.Append(",\"p95\":").Append(Num(NearestRank(values, 0.95)));
        sb.Append(",\"max\":").Append(Num(values[values.Length - 1])).Append('}');
    }

    // Nearest-rank percentile: the smallest value with at least p of the samples at or below it.
    private static double NearestRank(double[] sorted, double p)
    {
        var rank = (int)System.Math.Ceiling(p * sorted.Length);
        return sorted[System.Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
