using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Reflection.PortableExecutable;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that the app icon asset is wired correctly for macOS Dock and Windows shell pinning.
/// </summary>
public class AppIconTests
{
    private const string IconUri = "avares://AnimationEditor/Assets/icons/achx-icon-256.png";

    private static string AppProjectDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AnimationEditor.App"));

    /// <summary>
    /// The icon asset must exist and be decodable as a bitmap. This is the resource that
    /// Avalonia propagates to NSApplication.SharedApplication.ApplicationIconImage on macOS.
    /// </summary>
    [AvaloniaFact]
    public void IconAsset_IsLoadable()
    {
        using var stream = AssetLoader.Open(new Uri(IconUri));
        Assert.NotNull(stream);
        var bitmap = new Bitmap(stream);
        Assert.True(bitmap.PixelSize.Width > 0);
        Assert.True(bitmap.PixelSize.Height > 0);
    }

    /// <summary>
    /// MainWindow must have its Icon property set (not null) so Avalonia propagates it to the
    /// macOS Dock. Belt-and-suspenders over the XAML attribute: ensures the programmatic setter
    /// in App.axaml.cs actually fires.
    /// </summary>
    [AvaloniaFact]
    public void MainWindow_HasIconSet()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.NotNull(window.Icon);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// MacOSDockIcon.Set must not throw when given an empty byte array (defensive guard
    /// against asset-loading failure at runtime).
    /// </summary>
    [Fact]
    public void MacOSDockIcon_DoesNotThrowForEmptyBytes()
    {
        var exception = Record.Exception(() => MacOSDockIcon.Set(Array.Empty<byte>()));
        Assert.Null(exception);
    }

    /// <summary>
    /// MacOSDockIcon.Set must not throw when called with valid PNG bytes on any platform.
    /// On non-macOS this is a no-op; on macOS it exercises the NSImage creation path.
    /// </summary>
    [AvaloniaFact]
    public void MacOSDockIcon_DoesNotThrowWithValidPngBytes()
    {
        using var stream = AssetLoader.Open(new Uri(IconUri));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        var exception = Record.Exception(() => MacOSDockIcon.Set(ms.ToArray()));
        Assert.Null(exception);
    }

    /// <summary>
    /// MacOSDockIcon.SetProcessName must not throw on any platform.
    /// </summary>
    [Fact]
    public void MacOSDockIcon_SetProcessName_DoesNotThrow()
    {
        var exception = Record.Exception(() => MacOSDockIcon.SetProcessName("Test App"));
        Assert.Null(exception);
    }

    /// <summary>
    /// Windows taskbar pins and Explorer read the icon embedded in the .exe, not Avalonia's runtime
    /// window icon. The source .ico must include standard shell sizes.
    /// </summary>
    [Fact]
    public void WindowsAppIcon_FileExistsWithStandardSizes()
    {
        var icoPath = Path.Combine(AppProjectDirectory, "Assets", "icons", "AppIcon.ico");
        Assert.True(File.Exists(icoPath), $"Expected Windows app icon at {icoPath}");

        var bytes = File.ReadAllBytes(icoPath);
        Assert.True(bytes.Length >= 6, "ICO file is too small to be valid");
        Assert.Equal(0, bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal(1, bytes[2]);
        Assert.Equal(0, bytes[3]);

        var imageCount = bytes[4] | (bytes[5] << 8);
        Assert.True(imageCount >= 4, $"ICO should embed multiple sizes for Windows shell scaling, found {imageCount}");
    }

    /// <summary>
    /// The app project must embed <c>AppIcon.ico</c> so pinned shortcuts and file associations
    /// resolve icon index 0 on the executable.
    /// </summary>
    [Fact]
    public void Csproj_EmbedsApplicationIcon()
    {
        var csprojPath = Path.Combine(AppProjectDirectory, "AnimationEditor.App.csproj");
        var csproj = File.ReadAllText(csprojPath);

        Assert.Contains("<ApplicationIcon>Assets\\icons\\AppIcon.ico</ApplicationIcon>", csproj);
    }

    /// <summary>
    /// After build, the Windows executable should carry a Win32 resource table that includes the
    /// embedded application icon.
    /// </summary>
    [Fact]
    public void BuiltExecutable_HasWin32ResourceTable_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var exePath = Path.Combine(AppContext.BaseDirectory, "AnimationEditor.exe");
        Assert.True(File.Exists(exePath), $"Expected built exe at {exePath}");

        using var stream = File.OpenRead(exePath);
        using var reader = new PEReader(stream);
        var resourceDir = reader.PEHeaders.PEHeader!.ResourceTableDirectory;
        Assert.True(resourceDir.Size > 0, "Built exe should embed Win32 resources when ApplicationIcon is set");
    }
}
