using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using FlatRedBall.IO;
using System;
using System.IO;
using FilePath = FlatRedBall.IO.FilePath;

namespace AnimationEditor.Core.IO
{
    public class IoManager : Singleton<IoManager>
    {
        /// <summary>Raised when saving the companion file fails. The app layer should display the error.</summary>
        public event Action<string, Exception> SaveFailed;

        // ── Recovery file ─────────────────────────────────────────────────────

        /// <summary>
        /// Path used for the crash-recovery file. Defaults to a fixed file in the system temp
        /// folder. Override in tests to avoid cross-test contamination.
        /// </summary>
        public string RecoveryFilePath { get; set; } =
            Path.Combine(Path.GetTempPath(), "AnimationEditor_Recovery.achx");

        /// <summary>Returns true when a recovery file exists at <see cref="RecoveryFilePath"/>.</summary>
        public bool RecoveryFileExists() => File.Exists(RecoveryFilePath);

        /// <summary>
        /// Writes the current animation chain list to <see cref="RecoveryFilePath"/> atomically
        /// (write to a sibling .tmp file, then replace). Fires <see cref="SaveFailed"/> on error;
        /// never throws.
        /// </summary>
        public void WriteRecoveryFile()
        {
            var acls = ProjectManager.Self.AnimationChainListSave;
            if (acls == null) return;

            var tmpPath = RecoveryFilePath + ".tmp";
            try
            {
                ProjectManager.Self.SaveAnimationChainList(tmpPath);
                File.Move(tmpPath, RecoveryFilePath, overwrite: true);
            }
            catch (Exception e)
            {
                try { File.Delete(tmpPath); } catch { }
                SaveFailed?.Invoke("Could not write recovery file " + RecoveryFilePath + "\n\n" + e, e);
            }
        }

        /// <summary>Deletes the recovery file if it exists. Never throws.</summary>
        public void DeleteRecoveryFile()
        {
            try { File.Delete(RecoveryFilePath); } catch { }
        }

        private FilePath GetCompanionFileFor(FilePath fileName)
        {
            return (FilePath)(fileName.RemoveExtension() + ".aeproperties");
        }

        public void SaveCompanionFileFor(FilePath fileName, AESettingsSave settings)
        {
            var location = GetCompanionFileFor(fileName);
            try
            {
                FileManager.XmlSerialize(settings, location.FullPath);
            }
            catch (Exception e)
            {
                SaveFailed?.Invoke("Could not save companion file " + location + "\n\n" + e, e);
            }
        }

        public void LoadAndApplyCompanionFileFor(string achxFile)
        {
            var fileToLoad = GetCompanionFileFor(achxFile);

            if (!fileToLoad.Exists()) return;

            AESettingsSave loadedInstance = null;
            try
            {
                loadedInstance = FileManager.XmlDeserialize<AESettingsSave>(fileToLoad.FullPath);
            }
            catch
            {
                return;
            }

            if (loadedInstance != null)
            {
                ApplySettings(loadedInstance);
            }
        }

        private void ApplySettings(AESettingsSave settings)
        {
            AppState.Self.UnitType = settings.UnitType;
            AppState.Self.IsSnapToGridChecked = settings.SnapToGrid;
            AppState.Self.GridSize = settings.GridSize;

            // Expanded nodes and guide lines are stored in settings but applied by
            // the UI layer — raise an event so the tree and preview panels can pick them up.
            SettingsLoaded?.Invoke(settings);
        }

        /// <summary>Raised after a companion file is loaded. Provides the full settings object
        /// so the UI layer can apply expanded tree nodes, guide lines, etc.</summary>
        public event Action<AESettingsSave> SettingsLoaded;
    }
}
