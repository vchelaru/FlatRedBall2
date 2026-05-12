using System;
using System.IO;

namespace FlatRedBall2.Content;

/// <summary>
/// Production <see cref="IFileWatcher"/> backed by <see cref="FileSystemWatcher"/>. Watches a
/// single file by directory + filename. Events fire on a threadpool thread; <see cref="ContentWatcher"/>
/// handles marshalling back to the game thread.
/// </summary>
internal sealed class FileSystemFileWatcher : IFileWatcher
{
    private readonly FileSystemWatcher _watcher;

    public event Action? Changed;

    public FileSystemFileWatcher(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var name = Path.GetFileName(path);
        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => Changed?.Invoke();
        _watcher.Created += (_, _) => Changed?.Invoke();
        _watcher.Renamed += (_, _) => Changed?.Invoke();
    }

    public void Dispose() => _watcher.Dispose();
}
