namespace AnimationEditor.Editing;

/// <summary>Puts files back as they were before a batch wrote to them, for a batch that then fails (#1978).</summary>
/// <param name="saveAsideFolder">Where the old content of a file that cannot be put back goes; the temp folder by default.</param>
public sealed class FileRollback(string? saveAsideFolder = null)
{
    // Bytes is null for a file that did not exist.
    private sealed record Original(string Path, byte[]? Bytes, DateTime LastWriteUtc, FileAttributes Attributes);

    private readonly List<Original> _originals = [];

    /// <summary>Keeps what <paramref name="path"/> holds now. Call before writing it; only a path's first call counts.</summary>
    public void Remember(string path)
    {
        if (_originals.Any(o => o.Path == path)) return;
        _originals.Add(File.Exists(path)
            ? new(path, File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path), File.GetAttributes(path))
            : new(path, null, default, default));
    }

    /// <summary>Lets the written files stand.</summary>
    public void Forget() => _originals.Clear();

    /// <summary>
    /// Restores every remembered file and forgets it; never throws. A file that could not be restored gives one line saying so; its
    /// old content, if it had any, is saved to the temp folder that line names.
    /// </summary>
    public IReadOnlyList<string> Restore()
    {
        var failures = new List<string>();
        // Newest first, so two spellings of one path end at the older content.
        foreach (var original in Enumerable.Reverse(_originals))
        {
            try { Restore(original); }
            catch (Exception ex)
            {
                failures.Add($"{original.Path} could not be put back as it was: {ex.Message}{SaveAside(original)}");
            }
        }
        Forget();
        return failures;
    }

    private static void Restore(Original original)
    {
        var path = original.Path;
        if (original.Bytes is null)
        {
            if (!File.Exists(path)) return;
            File.SetAttributes(path, FileAttributes.Normal); // a copy of a read-only file is read-only
            File.Delete(path);
            return;
        }

        if (File.Exists(path) && File.GetAttributes(path) == original.Attributes
            && File.GetLastWriteTimeUtc(path) == original.LastWriteUtc
            && File.ReadAllBytes(path).AsSpan().SequenceEqual(original.Bytes)) return;

        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllBytes(path, original.Bytes);
        File.SetLastWriteTimeUtc(path, original.LastWriteUtc);
        File.SetAttributes(path, original.Attributes);
    }

    private string SaveAside(Original original)
    {
        if (original.Bytes is null) return string.Empty;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(saveAsideFolder ?? Path.GetTempPath(), $"aecli-{Guid.NewGuid():N}")).FullName;
            var copy = Path.Combine(folder, Path.GetFileName(original.Path));
            File.WriteAllBytes(copy, original.Bytes);
            return $" Its old content is in {copy}.";
        }
        catch (Exception ex)
        {
            return $" Its old content could not be saved: {ex.Message}";
        }
    }
}
