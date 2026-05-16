namespace AnimationEditor.Core.IO;

/// <summary>
/// Detects Git merge-conflict markers in .achx file content so the editor can
/// surface a clear, actionable error instead of a generic XML parse failure.
/// </summary>
internal static class AchxConflictMarkerDetector
{
    // Git uses exactly these strings; they always appear at the start of a line.
    private const string ConflictStart = "<<<<<<<";
    private const string ConflictMid   = "=======";
    private const string ConflictEnd   = ">>>>>>>";

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="content"/> contains any of the
    /// three standard Git conflict-marker strings (<c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c>,
    /// <c>=======</c>, <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c>).
    /// </summary>
    public static bool HasConflictMarkers(string content) =>
        content.Contains(ConflictStart) ||
        content.Contains(ConflictMid)   ||
        content.Contains(ConflictEnd);

    /// <summary>
    /// Returns the user-facing error message to display when conflict markers are detected.
    /// </summary>
    public static string ConflictMarkerMessage =>
        "This file contains unresolved Git conflict markers — " +
        "resolve them in your editor or with `git mergetool` and save again.";
}
