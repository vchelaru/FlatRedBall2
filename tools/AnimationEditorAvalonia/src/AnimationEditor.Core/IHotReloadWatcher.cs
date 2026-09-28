using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.HotReload
{
    /// <summary>
    /// Watches an .achx file and its referenced PNGs for on-disk changes and fires
    /// typed events. The implementation handles debounce, own-save cooldown, atomic-write
    /// pattern detection, and reference list updates after an .achx reload.
    /// </summary>
    public interface IHotReloadWatcher : IDisposable
    {
        /// <summary>Fired when the .achx file itself changes on disk. Arg: full path.</summary>
        event Action<string> AchxChangedOnDisk;

        /// <summary>Fired when a referenced PNG changes on disk. Arg: full absolute path.</summary>
        event Action<string> PngChangedOnDisk;

        /// <summary>Fired when the .achx file is deleted from disk. Arg: full path.</summary>
        event Action<string> AchxDeletedOnDisk;

        /// <summary>Whether hot reload is active. Toggle from the UI.</summary>
        bool IsEnabled { get; set; }

        /// <summary>Begin watching the .achx and its referenced PNGs.</summary>
        void StartWatching(string achxPath, IEnumerable<string> pngPaths);

        /// <summary>Update the PNG watch list after an .achx reload (handles ref list drift).</summary>
        void UpdatePngList(IEnumerable<string> newPngPaths);

        /// <summary>Stop all watching.</summary>
        void StopWatching();

        /// <summary>Runs <paramref name="write"/> (our own write of <paramref name="filePath"/>) and
        /// records what landed, so the resulting FSW event is recognised as ours by the file's
        /// content and suppressed, while an external write still fires. The watcher never samples
        /// the file mid-write: its content check waits for the save to finish.</summary>
        void RunOwnSave(string filePath, Action write);
    }
}
