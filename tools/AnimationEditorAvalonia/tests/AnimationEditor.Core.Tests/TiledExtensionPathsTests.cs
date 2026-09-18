using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TiledExtensionPathsTests
{
    [Fact]
    public void GetExtensionsFolder_NonWindows_AppendsForwardSlashExtensions()
    {
        string folder = TiledExtensionPaths.GetExtensionsFolder(isWindows: false, "/home/vic/.local/share/Tiled");

        Assert.Equal("/home/vic/.local/share/Tiled/extensions", folder);
    }

    [Fact]
    public void GetExtensionsFolder_Windows_AppendsBackslashExtensions()
    {
        string folder = TiledExtensionPaths.GetExtensionsFolder(isWindows: true, @"C:\Users\Vic\AppData\Local\Tiled");

        Assert.Equal(@"C:\Users\Vic\AppData\Local\Tiled\extensions", folder);
    }

    [Fact]
    public void GetTiledDataRoot_NonWindows_ReturnsHomeLocalShareTiledFolder()
    {
        string? root = TiledExtensionPaths.GetTiledDataRoot(isWindows: false, localAppData: null, homeDirectory: "/home/vic");

        Assert.Equal("/home/vic/.local/share/Tiled", root);
    }

    [Fact]
    public void GetTiledDataRoot_NonWindowsMissingHome_ReturnsNull()
    {
        string? root = TiledExtensionPaths.GetTiledDataRoot(isWindows: false, localAppData: null, homeDirectory: null);

        Assert.Null(root);
    }

    [Fact]
    public void GetTiledDataRoot_Windows_ReturnsLocalAppDataTiledFolder()
    {
        string? root = TiledExtensionPaths.GetTiledDataRoot(
            isWindows: true, localAppData: @"C:\Users\Vic\AppData\Local", homeDirectory: null);

        Assert.Equal(@"C:\Users\Vic\AppData\Local\Tiled", root);
    }

    [Fact]
    public void GetTiledDataRoot_WindowsMissingLocalAppData_ReturnsNull()
    {
        string? root = TiledExtensionPaths.GetTiledDataRoot(isWindows: true, localAppData: null, homeDirectory: null);

        Assert.Null(root);
    }
}
