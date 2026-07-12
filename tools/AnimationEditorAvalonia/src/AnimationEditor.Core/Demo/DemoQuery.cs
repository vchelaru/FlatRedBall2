using System;

namespace AnimationEditor.Core.Demo;

/// <summary>
/// Parses <c>?demo=&lt;name&gt;</c> from a URL. For test / temporary local hooks only —
/// do not wire this into shipping <c>App.BuildView</c>.
/// </summary>
internal static class DemoQuery
{
    public static string? TryGetDemoName(string pageUrl)
    {
        if (string.IsNullOrEmpty(pageUrl)) return null;

        // Uri.TryCreate handles absolute http(s) URLs from Avalonia Browser's location.href.
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri))
            return null;

        var query = uri.Query;
        if (string.IsNullOrEmpty(query) || query.Length < 2) return null;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = eq < 0 ? pair : pair[..eq];
            if (!key.Equals("demo", StringComparison.OrdinalIgnoreCase)) continue;

            var value = eq < 0 || eq == pair.Length - 1
                ? ""
                : Uri.UnescapeDataString(pair[(eq + 1)..]);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        return null;
    }
}
