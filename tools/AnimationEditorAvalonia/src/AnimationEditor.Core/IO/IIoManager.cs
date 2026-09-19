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
        /// files <c>ProjectManager.SaveAnimationChainListAsync</c> should sync this project's
        /// animation chains into on save. Empty when no companion file exists or none are set.
        /// </summary>
        IReadOnlyList<string> GetAssociatedTiledTilesetPaths(string achxFile);

        /// <summary>
        /// Records <paramref name="tsxFile"/> (stored relative to <paramref name="achxFile"/>'s
        /// folder) as a tileset that should be kept in sync with this .achx/.achj's animation
        /// chains. A no-op if already associated.
        /// </summary>
        void AddAssociatedTiledTilesetPath(string achxFile, string tsxFile);

        void WriteRecoveryFile(AnimationChainListSave? animationChainListSave);
        void DeleteRecoveryFile();
        bool RecoveryFileExists();

        /// <summary>
        /// Reads back a recovery file written by <see cref="WriteRecoveryFile"/>. Returns null
        /// when no recovery file exists or it fails to parse — callers should treat a null result
        /// the same as "nothing to restore" and delete the stale file.
        /// </summary>
        AnimationChainListSave? TryReadRecoveryFile();
    }
}
