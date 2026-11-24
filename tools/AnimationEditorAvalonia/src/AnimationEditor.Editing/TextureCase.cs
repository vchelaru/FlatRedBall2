namespace AnimationEditor.Editing;

/// <summary>Finds texture names that only match their file where file names ignore case (Windows, macOS).</summary>
internal static class TextureCase
{
    /// <summary>
    /// The name as it is spelled on disk when it differs from <paramref name="name"/> only by case, or null. Every folder in
    /// the name is compared too. Absolute names are not checked, since they only work on one machine anyway.
    /// </summary>
    public static string? NameOnDisk(string name, string folder)
    {
        if (Path.IsPathRooted(name)) return null;

        var parts = name.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        bool differs = false;
        try
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] is not ("" or "." or ".."))
                {
                    string? onDisk = null;
                    foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
                    {
                        var found = Path.GetFileName(entry);
                        if (!string.Equals(found, parts[i], StringComparison.OrdinalIgnoreCase)) continue;
                        onDisk = found;
                        if (found == parts[i]) break; // an exact match wins where both can exist
                    }
                    if (onDisk is not null && onDisk != parts[i])
                    {
                        parts[i] = onDisk;
                        differs = true;
                    }
                }
                folder = Path.Combine(folder, parts[i]);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // a folder that cannot be listed
        }
        return differs ? string.Join('/', parts) : null;
    }
}
