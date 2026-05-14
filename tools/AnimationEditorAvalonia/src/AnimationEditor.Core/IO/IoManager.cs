using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;
using System;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO
{
    public class IoManager : IIoManager
    {
        private readonly IAppState _appState;

        public IoManager(IAppState appState)
        {
            _appState = appState;
        }
        /// <summary>Raised when saving the companion file fails. The app layer should display the error.</summary>
        public event Action<string, Exception>? SaveFailed;
        public string RecoveryFilePath { get; set; } =
            Path.Combine(Path.GetTempPath(), "AnimationEditor_Recovery.achx");

        private FilePath GetCompanionFileFor(FilePath fileName)
        {
            return new FilePath(fileName.RemoveExtension().FullPath + ".aeproperties");
        }

        public void SaveCompanionFileFor(FilePath fileName, AESettingsSave settings)
        {
            var location = GetCompanionFileFor(fileName);
            try
            {
                XmlFile.Serialize(settings, location.FullPath);
            }
            catch (Exception e)
            {
                SaveFailed?.Invoke("Could not save companion file " + location + "\n\n" + e, e);
            }
        }

        public void LoadAndApplyCompanionFileFor(string achxFile)
        {
            var fileToLoad = GetCompanionFileFor(new FilePath(achxFile));

            if (!fileToLoad.Exists()) return;

            AESettingsSave? loadedInstance = null;
            try
            {
                loadedInstance = XmlFile.Deserialize<AESettingsSave>(fileToLoad.FullPath);
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

        public void WriteRecoveryFile(AnimationChainListSave? animationChainListSave)
        {
            if (animationChainListSave == null) return;

            string tempPath = RecoveryFilePath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(RecoveryFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                animationChainListSave.Save(tempPath);
                File.Move(tempPath, RecoveryFilePath, overwrite: true);
            }
            catch (Exception e)
            {
                SaveFailed?.Invoke("Could not save recovery file " + RecoveryFilePath + "\n\n" + e, e);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        public void DeleteRecoveryFile()
        {
            if (File.Exists(RecoveryFilePath))
            {
                File.Delete(RecoveryFilePath);
            }
        }

        public bool RecoveryFileExists() => File.Exists(RecoveryFilePath);

        private void ApplySettings(AESettingsSave settings)
        {
            _appState.IsSnapToGridChecked = settings.SnapToGrid;
            _appState.GridSize = settings.GridSize;

            // Expanded nodes and guide lines are stored in settings but applied by
            // the UI layer — raise an event so the tree and preview panels can pick them up.
            SettingsLoaded?.Invoke(settings);
        }

        /// <summary>Raised after a companion file is loaded. Provides the full settings object
        /// so the UI layer can apply expanded tree nodes, guide lines, etc.</summary>
        public event Action<AESettingsSave>? SettingsLoaded;
    }
}
