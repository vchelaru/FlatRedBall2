using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectThumbnailCacheLocationTests
{
    [Fact]
    public void ForApplicationDataRoot_PlacesDirectoryUnderAnimationEditorSubfolder()
    {
        // Backslash literal proves the cross-platform FilePath handling (see AppSettingsLocationTests).
        var result = ProjectThumbnailCacheLocation.ForApplicationDataRoot(@"C:\Users\dev\AppData\Roaming");

        Assert.Equal(@"C:\Users\dev\AppData\Roaming\AnimationEditor\ThumbnailCache".Replace('\\', '/'),
            result.Replace('\\', '/'));
    }
}
