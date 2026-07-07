using AnimationEditor.App.Services;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// End-to-end coverage of the PNG Diff pipeline (issue #606) against a real temporary git
/// repository: git history retrieval, blob fetch, PNG decode, pixel diff, and region merge. Skips
/// when the <c>git</c> executable is unavailable so the suite still runs in git-less environments.
/// </summary>
public class PngBlameServiceIntegrationTests : IDisposable
{
    private readonly string _repoDir =
        Path.Combine(Path.GetTempPath(), "ae-blame-" + Guid.NewGuid().ToString("N"));

    public PngBlameServiceIntegrationTests() => Directory.CreateDirectory(_repoDir);

    public void Dispose()
    {
        try { Directory.Delete(_repoDir, recursive: true); }
        catch (IOException) { /* git may briefly hold a handle; best-effort cleanup */ }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void ComputeRegions_SecondCommitChangedOnePixel_FindsThatRegion()
    {
        SkipIfNoGit();

        // Two committed versions of a 4×4 sheet: all-red, then the same with pixel (0,0) turned blue.
        string png = Path.Combine(_repoDir, "sheet.png");
        InitRepo();
        File.WriteAllBytes(png, SolidRed4x4());
        Commit("Add sheet");
        File.WriteAllBytes(png, Red4x4WithBlueCorner());
        Commit("Recolor corner");

        var service = new PngBlameService();
        var result = service.Load(png);

        // Clean tree after committing → no working-tree entry, just the two commits (newest first).
        Assert.Equal(Core.Git.GitHistoryStatus.Ok, result.Status);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Recolor corner", result.Entries[0].Subject);

        // Newest revision vs. its parent: exactly the recolored pixel changed.
        var regions = service.ComputeRegions(entryIndex: 0, tolerance: 8, distanceThreshold: 2);

        Assert.Single(regions);
        Assert.Equal(0, regions[0].MinX);
        Assert.Equal(0, regions[0].MinY);
        Assert.Equal(0, regions[0].MaxX);
        Assert.Equal(0, regions[0].MaxY);
    }

    [Fact]
    public void GetRevisionImage_ReturnsSelectedRevisionPixels_NotAlwaysCurrent()
    {
        SkipIfNoGit();

        // Same two committed versions: all-red, then pixel (0,0) recolored blue.
        string png = Path.Combine(_repoDir, "sheet.png");
        InitRepo();
        File.WriteAllBytes(png, SolidRed4x4());
        Commit("Add sheet");
        File.WriteAllBytes(png, Red4x4WithBlueCorner());
        Commit("Recolor corner");

        var service = new PngBlameService();
        service.Load(png);

        // The displayed image must follow the selected revision: the newest shows the blue corner,
        // the oldest still shows red — not always the current file (the whole point of the Diff view).
        var newest = service.GetRevisionImage(entryIndex: 0);
        var oldest = service.GetRevisionImage(entryIndex: 1);

        Assert.NotNull(newest);
        Assert.NotNull(oldest);
        // Pixel (0,0) is the first RGBA quad: R,G,B,A at indices 0..3.
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, newest!.Rgba[..4]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, oldest!.Rgba[..4]);
    }

    // ── git-backed fixture helpers ────────────────────────────────────────────

    private void InitRepo()
    {
        Assert.Equal(0, RunGit("init", "-q", "-b", "main"));
    }

    private void Commit(string message)
    {
        Assert.Equal(0, RunGit("add", "-A"));
        // Inline identity + no signing so the commit never depends on the host's git config.
        Assert.Equal(0, RunGit("-c", "user.name=Test", "-c", "user.email=test@example.com",
            "-c", "commit.gpgsign=false", "commit", "-q", "-m", message));
    }

    private int RunGit(params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repoDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static void SkipIfNoGit()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            proc.WaitForExit();
            if (proc.ExitCode != 0) Assert.Skip("git returned a non-zero exit code.");
        }
        catch (Exception)
        {
            Assert.Skip("git executable is not available.");
        }
    }

    private static byte[] SolidRed4x4() => EncodePng(bmp => bmp.Erase(new SKColor(255, 0, 0, 255)));

    private static byte[] Red4x4WithBlueCorner() => EncodePng(bmp =>
    {
        bmp.Erase(new SKColor(255, 0, 0, 255));
        bmp.SetPixel(0, 0, new SKColor(0, 0, 255, 255));
    });

    private static byte[] EncodePng(Action<SKBitmap> paint)
    {
        using var bmp = new SKBitmap(4, 4, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        paint(bmp);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
