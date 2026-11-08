using AnimationEditor.Core.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class LogFileLocationTests
{
    // FilePath normalizes directory separators to forward slashes, so expected values use '/'.
    [Fact]
    public void NextToExecutable_PlacesLogTxtInBaseDirectory()
    {
        var result = LogFileLocation.NextToExecutable(@"C:\Apps\AnimationEditor");

        Assert.Equal("C:/Apps/AnimationEditor/log.txt", result.FullPath);
    }

    [Fact]
    public void ForApplicationDataRoot_PlacesLogUnderAnimationEditorSubfolder()
    {
        var result = LogFileLocation.ForApplicationDataRoot(@"C:\Users\dev\AppData\Roaming");

        Assert.Equal("C:/Users/dev/AppData/Roaming/AnimationEditor/log.txt", result.FullPath);
    }

    [Fact]
    public void Resolve_WhenExeDirWritable_UsesNextToExecutable()
    {
        var result = LogFileLocation.Resolve(
            @"C:\Apps\AnimationEditor", @"C:\Users\dev\AppData\Roaming",
            isDirectoryWritable: _ => true);

        Assert.Equal("C:/Apps/AnimationEditor/log.txt", result.FullPath);
    }

    // Velopack replaces the whole `current` folder on every update, so a log there is lost exactly
    // when an update misbehaves.
    [Fact]
    public void Resolve_InsideAVelopackCurrentFolder_UsesApplicationDataEvenWhenWritable()
    {
        var result = LogFileLocation.Resolve(
            @"C:\Users\dev\AppData\Local\AnimationEditor\current\", @"C:\Users\dev\AppData\Roaming",
            isDirectoryWritable: _ => true,
            fileExists: path => path == "C:/Users/dev/AppData/Local/AnimationEditor/Update.exe");

        Assert.Equal("C:/Users/dev/AppData/Roaming/AnimationEditor/log.txt", result.FullPath);
    }

    [Fact]
    public void Resolve_InACurrentFolderWithoutUpdateExe_StaysNextToExecutable()
    {
        var result = LogFileLocation.Resolve(
            @"C:\dev\current", @"C:\Users\dev\AppData\Roaming",
            isDirectoryWritable: _ => true,
            fileExists: _ => false);

        Assert.Equal("C:/dev/current/log.txt", result.FullPath);
    }

    [Fact]
    public void Resolve_WhenExeDirNotWritable_FallsBackToApplicationData()
    {
        // Simulates install under a write-protected dir like Program Files.
        var result = LogFileLocation.Resolve(
            @"C:\Program Files\AnimationEditor", @"C:\Users\dev\AppData\Roaming",
            isDirectoryWritable: _ => false);

        Assert.Equal("C:/Users/dev/AppData/Roaming/AnimationEditor/log.txt", result.FullPath);
    }
}
