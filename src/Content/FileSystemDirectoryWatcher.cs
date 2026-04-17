using System;
using System.IO;

namespace FlatRedBall2.Content;

/// <summary>
/// Production <see cref="IDirectoryWatcher"/> backed by a recursive <see cref="FileSystemWatcher"/>.
/// Reports changed file paths relative to the watched root.
/// </summary>
internal sealed class FileSystemDirectoryWatcher : IDirectoryWatcher
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _rootPath;

    public event Action<string>? Changed;

    public FileSystemDirectoryWatcher(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _watcher = new FileSystemWatcher(_rootPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                         | NotifyFilters.CreationTime | NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, e) => Fire(e.FullPath);
        _watcher.Created += (_, e) => Fire(e.FullPath);
        _watcher.Renamed += (_, e) => Fire(e.FullPath);
    }

    private void Fire(string fullPath)
    {
        // Directory events are noisy; only surface file paths to keep callbacks predictable.
        if (Directory.Exists(fullPath)) return;
        var rel = Path.GetRelativePath(_rootPath, fullPath);
        Changed?.Invoke(rel);
    }

    public void Dispose() => _watcher.Dispose();
}
