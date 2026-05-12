using AnimationEditor.Core.Data;
using System;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO
{
    public interface IIoManager
    {
        event Action<string, Exception> SaveFailed;
        event Action<AESettingsSave> SettingsLoaded;

        void SaveCompanionFileFor(FilePath fileName, AESettingsSave settings);
        void LoadAndApplyCompanionFileFor(string achxFile);
    }
}
