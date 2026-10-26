using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1552: an image too big to decode shows a clear error banner (naming size and limit), and the
/// inspector's pixel region fields no longer stop at +-16384, since a frame region may lie outside
/// the texture (a wrapping texture reads past its edge).
/// </summary>
public class OversizedImageTests
{
    private static MainWindow CreateWindow(TestServices ctx)
    {
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void WireframeLoadOfOversizedImage_ShowsErrorBannerNamingSizeAndLimit()
    {
        var ctx = TestHelpers.BuildServices();
        var window = CreateWindow(ctx);
        string dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "huge.png");
        try
        {
            WriteHeaderOnlyPng(path, 24000, 24000);

            bool loaded = window.FindControl<WireframeControl>("WireframeCtrl")!.LoadTexture(path);
            Dispatcher.UIThread.RunJobs();

            Assert.False(loaded);
            var banner = window.FindControl<EditorNotificationOverlay>("Notifications")!;
            Assert.True(banner.ErrorBanner.IsVisible);
            Assert.Contains("24000 x 24000", banner.ErrorBannerText.Text);
            Assert.Contains("2 GB", banner.ErrorBannerText.Text);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaTheory]
    [InlineData("PropPixelX")]
    [InlineData("PropPixelY")]
    [InlineData("PropPixelW")]
    [InlineData("PropPixelH")]
    public void PixelRegionField_RangeIsNotCappedAtSixteenThousand(string name)
    {
        var ctx = TestHelpers.BuildServices();
        var window = CreateWindow(ctx);
        try
        {
            var field = window.FindControl<NumericUpDown>(name)!;

            // Programmatic Value sets are not clamped by Avalonia, so the range is asserted directly.
            Assert.True(field.Maximum > 16384m, $"{name}.Maximum is {field.Maximum}");
            field.Value = 20000;
            Assert.Equal(20000m, field.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("PropPixelX")]
    [InlineData("PropPixelY")]
    public void PixelPositionField_AllowsNegativeBeyondSixteenThousand(string name)
    {
        var ctx = TestHelpers.BuildServices();
        var window = CreateWindow(ctx);
        try
        {
            var field = window.FindControl<NumericUpDown>(name)!;

            Assert.True(field.Minimum < -16384m, $"{name}.Minimum is {field.Minimum}");
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("PropPixelW")]
    [InlineData("PropPixelH")]
    public void PixelSizeField_MinimumIsStillOne(string name)
    {
        var ctx = TestHelpers.BuildServices();
        var window = CreateWindow(ctx);
        try
        {
            Assert.Equal(1m, window.FindControl<NumericUpDown>(name)!.Minimum);
        }
        finally { window.Close(); }
    }

    // Declares the size in IHDR only; no pixel data, so the test never allocates gigabytes.
    private static void WriteHeaderOnlyPng(string path, int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; ihdr[9] = 6;
        WriteChunk(ms, "IHDR", ihdr);
        WriteChunk(ms, "IDAT", new byte[] { 0x78, 0x01, 0x00 });
        WriteChunk(ms, "IEND", System.Array.Empty<byte>());
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
        data.CopyTo(body, 4);
        s.Write(body);
        uint crc = 0xFFFFFFFF;
        foreach (byte b in body)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
        s.Write(crcBytes);
    }
}
