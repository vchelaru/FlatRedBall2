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
        /// Writes the crash-recovery copy of an unsaved document. Each document keeps its own file: the first one is
        /// <see cref="RecoveryFilePath"/>, or that name with a number when the name is taken.
        /// </summary>
        void WriteRecoveryFile(AnimationChainListSave? document);

        /// <summary>
        /// Deletes the recovery file of <paramref name="document"/>, if it has one, and no other document's. One that is in
        /// use stays, for <see cref="DeleteRecoveryFiles"/> to try again when the window closes.
        /// </summary>
        void DeleteRecoveryFile(AnimationChainListSave? document);

        /// <summary>Deletes the recovery file of every document written or read here. Another window's are left alone.</summary>
        void DeleteRecoveryFiles();

        /// <summary>Every recovery file on disk, the one written last at the end.</summary>
        IReadOnlyList<string> FindRecoveryFiles();

        /// <summary>
        /// Reads back a recovery file. It stays on disk as the file of the returned document, until that document is saved
        /// or discarded. A file that does not parse is deleted and null is returned.
        /// </summary>
        /// <exception cref="System.IO.IOException">The file is in use; it is left for the next start.</exception>
        AnimationChainListSave? TryReadRecoveryFile(string path);
    }
}
