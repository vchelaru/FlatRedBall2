using System.Collections.Generic;

namespace AnimationEditor.Core.Logging;

/// <summary>Formats the lines that open a log session.</summary>
public static class SessionHeader
{
    /// <summary>The process identity stamped on every log entry.</summary>
    public static string Context(int processId, string version) => $"pid={processId} v{version}";

    public static string Format(string version, string os, IReadOnlyList<string> args, string? exePath, string logPath) =>
        $"=== Process start === version={version} os={os} args=[{string.Join(" | ", args)}] " +
        $"exe={exePath ?? "(unknown)"} log={logPath}";
}
