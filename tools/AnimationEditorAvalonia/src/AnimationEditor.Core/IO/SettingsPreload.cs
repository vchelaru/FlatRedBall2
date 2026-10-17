using AnimationEditor.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Reads the settings file on the thread pool so startup can overlap that read (and the one-time
/// cost of warming up the JSON serializer) with work the UI thread has to do anyway. The window
/// <see cref="Take"/>s the result instead of reading the file itself.
/// </summary>
public sealed class SettingsPreload
{
    private readonly FilePath _path;
    private Task<AppSettingsModel>? _read;

    public SettingsPreload(FilePath path)
    {
        _path = path;
        _read = Task.Run(() => Read(path));
        Completion = _read;
    }

    /// <summary>Completes once the file has been read; <see cref="Take"/> waits on this itself.</summary>
    public Task Completion { get; }

    /// <summary>
    /// Returns the settings read for <paramref name="path"/>, or null when this preload was
    /// started for a different path or was already taken. One-shot on purpose: it is a startup
    /// snapshot, so a later window must read the file itself rather than get a stale copy.
    /// </summary>
    public AppSettingsModel? Take(FilePath path)
    {
        if (_path != path) return null;
        var read = Interlocked.Exchange(ref _read, null);
        return read?.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reads <paramref name="path"/>; a missing or unreadable file yields default settings, never
    /// an exception, so a damaged file can't stop the editor from starting.
    /// </summary>
    public static AppSettingsModel Read(FilePath path)
    {
        try
        {
            if (path.Exists())
                return JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(path.FullPath))
                       ?? new AppSettingsModel();
        }
        catch (Exception)
        {
            // Fall through to defaults.
        }
        return new AppSettingsModel();
    }
}
