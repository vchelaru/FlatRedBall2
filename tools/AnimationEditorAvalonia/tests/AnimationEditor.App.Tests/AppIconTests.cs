using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that the app icon asset is wired correctly so macOS can display it in the Dock.
/// </summary>
public class AppIconTests
{
    private const string IconUri = "avares://AnimationEditor/Assets/icons/achx-icon-256.png";

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
}
