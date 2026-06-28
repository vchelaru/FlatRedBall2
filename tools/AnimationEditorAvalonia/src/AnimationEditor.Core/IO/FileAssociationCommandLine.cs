namespace AnimationEditor.Core.IO;

/// <summary>
/// Parses the quoted-exe portion of a Windows <c>shell\open\command</c> registry value.
/// </summary>
public static class FileAssociationCommandLine
{
    /// <summary>
    /// Extracts the executable path from a value shaped like
    /// <c>"C:\Path\App.exe" "%1"</c>. Returns <c>null</c> when the string is empty or malformed.
    /// </summary>
    public static string? TryParseExePath(string? openCommand)
    {
        if (string.IsNullOrWhiteSpace(openCommand))
            return null;

        string trimmed = openCommand.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '"')
            return null;

        int closingQuote = trimmed.IndexOf('"', 1);
        if (closingQuote < 1)
            return null;

        return trimmed.Substring(1, closingQuote - 1);
    }
}
