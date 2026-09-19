using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.HotReload
{
    public sealed class NullHotReloadWatcher : IHotReloadWatcher
    {
        public static readonly NullHotReloadWatcher Instance = new();

        public event Action<string>? AchxChangedOnDisk { add { } remove { } }
        public event Action<string>? PngChangedOnDisk  { add { } remove { } }
        public event Action<string>? AchxDeletedOnDisk { add { } remove { } }
        public event Action<string>? TiledSyncChangedOnDisk     { add { } remove { } }
        public event Action<string>? AssociatedTsxChangedOnDisk { add { } remove { } }
        public bool IsEnabled { get; set; }
        public void StartWatching(string achxPath, IEnumerable<string> pngPaths, IEnumerable<string> tsxPaths) { }
        public void UpdatePngList(IEnumerable<string> newPngPaths) { }
        public void UpdateAssociatedTsxPaths(IEnumerable<string> newTsxPaths) { }
        public void StopWatching() { }
        public void RecordOwnSave(string filePath) { }
        public void Dispose() { }
    }
}
