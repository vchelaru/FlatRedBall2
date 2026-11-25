using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO
{
    public class IoManager : IIoManager
    {
        private readonly IAppState _appState;

        // The recovery file of each document written or read here. A document is its model object: an Untitled one has
        // no path, and its tab keeps the same object until it is saved or closed.
        private readonly Dictionary<AnimationChainListSave, string> _recoveryFiles = new(ReferenceEqualityComparer.Instance);

        /// <param name="recoveryFilePath">
        /// Where the crash-recovery copy of an unsaved document is written. Deliberately has no
        /// default: a default pointing at the real per-user temp file let every test that built
        /// its own <see cref="IoManager"/> write there, and the editor then offered that test
        /// data as a recovered document on its next launch.
        /// </param>
        public IoManager(IAppState appState, string recoveryFilePath)
        {
            _appState = appState;
            RecoveryFilePath = recoveryFilePath;
        }
        /// <summary>Raised when saving the companion file fails. The app layer should display the error.</summary>
        public event Action<string, Exception>? SaveFailed;

        public string RecoveryFilePath { get; set; }

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
            var loadedInstance = TryLoadCompanionSettings(achxFile);
            if (loadedInstance != null)
            {
                ApplySettings(loadedInstance);
            }
        }

        public AESettingsSave? TryLoadCompanionSettings(string achxFile)
        {
            var fileToLoad = GetCompanionFileFor(new FilePath(achxFile));

            if (!fileToLoad.Exists()) return null;

            try
            {
                return XmlFile.Deserialize<AESettingsSave>(fileToLoad.FullPath);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public void WriteRecoveryFile(AnimationChainListSave? document)
        {
            if (document == null) return;

            string path = _recoveryFiles.TryGetValue(document, out var own) ? own : UnusedRecoveryPath();
            string tempPath = path + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                document.Save(tempPath);
                File.Move(tempPath, path, overwrite: true);
                _recoveryFiles[document] = path;
            }
            catch (Exception e)
            {
                SaveFailed?.Invoke("Could not save recovery file " + path + "\n\n" + e, e);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private string UnusedRecoveryPath()
        {
            string path = RecoveryFilePath;
            for (int i = 1; File.Exists(path); i++)
                path = NumberedRecoveryPath(i.ToString());
            return path;
        }

        private string NumberedRecoveryPath(string number) =>
            Path.Combine(Path.GetDirectoryName(RecoveryFilePath) ?? "",
                $"{Path.GetFileNameWithoutExtension(RecoveryFilePath)} ({number}){Path.GetExtension(RecoveryFilePath)}");

        /// <inheritdoc/>
        public void DeleteRecoveryFile(AnimationChainListSave? document)
        {
            if (document != null && _recoveryFiles.TryGetValue(document, out var path) && TryDelete(path))
                _recoveryFiles.Remove(document);
        }

        private static bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public void DeleteRecoveryFiles()
        {
            foreach (var document in _recoveryFiles.Keys.ToList())
                DeleteRecoveryFile(document);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> FindRecoveryFiles()
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(RecoveryFilePath))!);
            if (!directory.Exists) return [];
            return directory.EnumerateFiles(Path.GetFileName(RecoveryFilePath))
                .Concat(directory.EnumerateFiles(Path.GetFileName(NumberedRecoveryPath("*"))))
                .OrderBy(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .ToList();
        }

        /// <inheritdoc/>
        public AnimationChainListSave? TryReadRecoveryFile(string path)
        {
            try
            {
                var recovered = AnimationChainListSave.FromFile(path);
                // A file an editor before #1952 left behind is in UV. An unsaved document's textures are absolute paths.
                TextureCoordinateConverter.ToPixels(recovered, TextureCoordinateConverter.SizesIn(string.Empty));
                _recoveryFiles[recovered] = path;
                return recovered;
            }
            catch (Exception e) when (e is not (IOException or UnauthorizedAccessException))
            {
                TryDelete(path);
                return null;
            }
        }

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
