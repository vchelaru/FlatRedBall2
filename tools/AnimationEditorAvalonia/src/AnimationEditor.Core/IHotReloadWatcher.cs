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

        /// <summary>
        /// Fired when the .achx's <c>.tiledsync</c> companion file (issue #1136) changes on disk
        /// -- e.g. a teammate's git pull added/changed a Tiled tileset association. Arg: full
        /// path. This is purely a live-feedback signal: <see cref="IO.IIoManager.GetAssociatedTiledTilesetPaths"/>
        /// already reads the file fresh on every save, so the change is picked up correctly on
        /// the next save either way (issue #1139).
        /// </summary>
        event Action<string> TiledSyncChangedOnDisk;

        /// <summary>
        /// Fired when a currently-associated <c>.tsx</c> tileset (see <see cref="UpdateAssociatedTsxPaths"/>)
        /// changes on disk. Arg: full path. Same live-feedback-only caveat as
        /// <see cref="TiledSyncChangedOnDisk"/> -- the sync pipeline already re-reads the .tsx
        /// fresh on every save.
        /// </summary>
        event Action<string> AssociatedTsxChangedOnDisk;

        /// <summary>Whether hot reload is active. Toggle from the UI.</summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Begin watching the .achx, its referenced PNGs, its <c>.tiledsync</c> companion file
        /// (derived automatically from <paramref name="achxPath"/>), and every currently
        /// associated <c>.tsx</c> tileset.
        /// </summary>
        void StartWatching(string achxPath, IEnumerable<string> pngPaths, IEnumerable<string> tsxPaths);

        /// <summary>Update the PNG watch list after an .achx reload (handles ref list drift).</summary>
        void UpdatePngList(IEnumerable<string> newPngPaths);

        /// <summary>Update the associated-.tsx watch list after the .tiledsync association list
        /// changes (new/removed association, or a reload).</summary>
        void UpdateAssociatedTsxPaths(IEnumerable<string> newTsxPaths);

        /// <summary>Stop all watching.</summary>
        void StopWatching();

        /// <summary>Call after our own save to suppress the resulting FSW event.</summary>
        void RecordOwnSave(string filePath);
    }
}
