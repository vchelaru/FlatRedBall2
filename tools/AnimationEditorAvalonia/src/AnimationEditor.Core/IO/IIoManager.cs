using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;
using System;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO
{
    public interface IIoManager
    {
        event Action<string, Exception> SaveFailed;
        event Action<AESettingsSave> SettingsLoaded;
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
