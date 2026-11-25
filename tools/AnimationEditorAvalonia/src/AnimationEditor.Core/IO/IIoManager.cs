using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO
{
    public interface IIoManager
    {
        event Action<string, Exception> SaveFailed;
        event Action<AESettingsSave> SettingsLoaded;

        /// <summary>
        /// Raised when a <c>.tiledsync</c> companion file exists on disk but fails to parse --
        /// distinct from "the file doesn't exist," which legitimately means "no associations
        /// configured" and stays silent. Without this, a corrupted <c>.tiledsync</c> silently
        /// disables Tiled sync for that .achx with zero indication anything is wrong (issue
        /// #1139). The first argument is the .achx path; the second is the exception.
        /// </summary>
        event Action<string, Exception> TiledSyncParseFailed;

        string RecoveryFilePath { get; set; }

        void SaveCompanionFileFor(FilePath fileName, AESettingsSave settings);
        void LoadAndApplyCompanionFileFor(string achxFile);

        /// <summary>
        /// Synchronously reads the companion settings for <paramref name="achxFile"/> without
        /// raising <see cref="SettingsLoaded"/>. Returns null when no companion file exists or
        /// it fails to deserialize. Callers that need to know the saved expand state (or other
        /// settings) before the first tree build — e.g. to avoid a collapse-then-restore flicker
        /// on tab switch — should use this instead of <see cref="LoadAndApplyCompanionFileFor"/>.
        /// </summary>
        AESettingsSave? TryLoadCompanionSettings(string achxFile);

        /// <summary>
        /// Returns the absolute paths of every Tiled <c>.tsx</c> tileset associated with
        /// <paramref name="achxFile"/> via <see cref="AddAssociatedTiledTilesetPath"/> -- the
        /// files a save should sync this document's
        /// animation chains into on save. Empty when no companion file exists or none are set.
        /// </summary>
        IReadOnlyList<string> GetAssociatedTiledTilesetPaths(string achxFile);

        /// <summary>
        /// Records <paramref name="tsxFile"/> (stored relative to <paramref name="achxFile"/>'s
        /// folder) as a tileset that should be kept in sync with this .achx/.achj's animation
        /// chains. A no-op if already associated. When it throws, nothing was stored and an existing
        /// <c>.tiledsync</c> is as it was.
        /// </summary>
        /// <exception cref="System.IO.InvalidDataException">The existing <c>.tiledsync</c> cannot be read.</exception>
        /// <exception cref="System.IO.IOException">The <c>.tiledsync</c> cannot be written.</exception>
        void AddAssociatedTiledTilesetPath(string achxFile, string tsxFile);

        /// <summary>
        /// Writes the crash-recovery copy of an unsaved document. Each document keeps its own file: the first one is
        /// <see cref="RecoveryFilePath"/>, or that name with a number when the name is taken.
        /// </summary>
        void WriteRecoveryFile(AnimationChainListSave? document);

        /// <summary>Deletes the recovery file of <paramref name="document"/>, if it has one, and no other document's.</summary>
        void DeleteRecoveryFile(AnimationChainListSave? document);

        /// <summary>Deletes the recovery file of every document written or read here. Another window's are left alone.</summary>
        void DeleteRecoveryFiles();

        /// <summary>Every recovery file on disk, the one written last at the end.</summary>
        IReadOnlyList<string> FindRecoveryFiles();

        /// <summary>
        /// Reads back a recovery file. It stays on disk as the file of the returned document, until that document is saved
        /// or discarded. A file that does not parse is deleted and null is returned.
        /// </summary>
        AnimationChainListSave? TryReadRecoveryFile(string path);
    }
}
