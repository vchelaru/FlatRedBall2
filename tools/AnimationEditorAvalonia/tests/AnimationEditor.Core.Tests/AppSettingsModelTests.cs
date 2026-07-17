using System.Text.Json;
using AnimationEditor.Core.Models;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="AppSettingsModel"/> recent-files list management.
/// This class has no singletons so no special isolation is required.
/// </summary>
public class AppSettingsModelTests
{
    [Fact]
    public void AddFile_FirstCall_ListContainsOneEntry()
    {
        var model = new AppSettingsModel();

        model.AddFile(new FilePath("C:/Games/hero.achx"));

        Assert.Single(model.RecentFiles);
    }

    [Fact]
    public void AddFile_AddsFileToFrontOfList()
    {
        var model = new AppSettingsModel();
        model.AddFile(new FilePath("C:/Games/first.achx"));
        model.AddFile(new FilePath("C:/Games/second.achx"));

        Assert.Equal("C:/Games/second.achx", model.RecentFiles[0]);
    }

    [Fact]
    public void AddFile_OlderFileMovedToSecondPosition()
    {
        var model = new AppSettingsModel();
        model.AddFile(new FilePath("C:/Games/first.achx"));
        model.AddFile(new FilePath("C:/Games/second.achx"));

        Assert.Equal("C:/Games/first.achx", model.RecentFiles[1]);
    }

    [Fact]
    public void AddFile_DeduplicatesExistingEntry_ListCountStaysTheSame()
    {
        var model = new AppSettingsModel();
        model.AddFile(new FilePath("C:/Games/hero.achx"));
        model.AddFile(new FilePath("C:/Games/other.achx"));
        int countBefore = model.RecentFiles.Count;

        model.AddFile(new FilePath("C:/Games/hero.achx"));

        Assert.Equal(countBefore, model.RecentFiles.Count);
    }

    [Fact]
    public void AddFile_MovesExistingEntryToFront()
    {
        var model = new AppSettingsModel();
        model.AddFile(new FilePath("C:/Games/hero.achx"));
        model.AddFile(new FilePath("C:/Games/other.achx"));

        model.AddFile(new FilePath("C:/Games/hero.achx"));

        Assert.Equal("C:/Games/hero.achx", model.RecentFiles[0]);
    }

    [Fact]
    public void AddFile_TrimsListToMaxTwentyItems()
    {
        var model = new AppSettingsModel();
        for (int i = 0; i < 25; i++)
        {
            model.AddFile(new FilePath($"C:/Games/file{i}.achx"));
        }

        Assert.Equal(20, model.RecentFiles.Count);
    }

    [Fact]
    public void AddFile_DropOldestEntryWhenExceedingMax()
    {
        var model = new AppSettingsModel();
        // Seed 20 entries starting at 0
        for (int i = 0; i < 20; i++)
        {
            model.AddFile(new FilePath($"C:/Games/file{i}.achx"));
        }
        // The very first file added (file0) is now last
        string firstAdded = "C:/Games/file0.achx";

        // Adding one more should drop file0
        model.AddFile(new FilePath("C:/Games/newfile.achx"));

        Assert.DoesNotContain(firstAdded, model.RecentFiles);
    }

    [Fact]
    public void AddFile_CaseInsensitiveDeduplicationViasFilePath()
    {
        var model = new AppSettingsModel();
        model.AddFile(new FilePath("C:/Games/Hero.achx"));

        // Same path, different casing
        model.AddFile(new FilePath("C:/Games/hero.achx"));

        Assert.Single(model.RecentFiles);
    }

    [Fact]
    public void RecentFiles_InitiallyEmpty()
    {
        var model = new AppSettingsModel();

        Assert.Empty(model.RecentFiles);
    }

    [Fact]
    public void AddFile_SameFileAddedTwice_ListCountIsOne()
    {
        var model = new AppSettingsModel();
        var path = new FilePath("C:/Games/run.achx");

        model.AddFile(path);
        model.AddFile(path);

        Assert.Single(model.RecentFiles);
    }

    [Fact]
    public void CanvasBackgroundArgb_DefaultsToNull()
    {
        var model = new AppSettingsModel();

        Assert.Null(model.CanvasBackgroundArgb);
    }

    [Fact]
    public void CanvasBackgroundArgb_SurvivesJsonRoundTrip()
    {
        // Packed 0xAARRGGBB — opaque dark slate.
        var model = new AppSettingsModel { CanvasBackgroundArgb = 0xFF102030 };

        var json = JsonSerializer.Serialize(model);
        var restored = JsonSerializer.Deserialize<AppSettingsModel>(json);

        Assert.Equal(0xFF102030u, restored!.CanvasBackgroundArgb);
    }

    [Fact]
    public void GuideLineArgb_DefaultsToNull()
    {
        var model = new AppSettingsModel();

        Assert.Null(model.GuideLineArgb);
    }

    [Fact]
    public void GuideLineArgb_SurvivesJsonRoundTrip()
    {
        // Packed 0xAARRGGBB — opaque magenta.
        var model = new AppSettingsModel { GuideLineArgb = 0xFFFF00FF };

        var json = JsonSerializer.Serialize(model);
        var restored = JsonSerializer.Deserialize<AppSettingsModel>(json);

        Assert.Equal(0xFFFF00FFu, restored!.GuideLineArgb);
    }

    [Fact]
    public void SuppressDefaultHandlerPrompt_DefaultsToFalse()
    {
        var model = new AppSettingsModel();

        Assert.False(model.SuppressDefaultHandlerPrompt);
    }

    [Fact]
    public void SuppressDefaultHandlerPrompt_SurvivesJsonRoundTrip()
    {
        var model = new AppSettingsModel { SuppressDefaultHandlerPrompt = true };

        var json = JsonSerializer.Serialize(model);
        var restored = JsonSerializer.Deserialize<AppSettingsModel>(json);

        Assert.True(restored!.SuppressDefaultHandlerPrompt);
    }

    [Fact]
    public void LastUpdateCheckUtc_DefaultsToNull()
    {
        var model = new AppSettingsModel();

        Assert.Null(model.LastUpdateCheckUtc);
    }

    [Fact]
    public void UpdateCheckCacheFields_SurviveJsonRoundTrip()
    {
        var model = new AppSettingsModel
        {
            LastUpdateCheckUtc = new System.DateTime(2026, 7, 17, 8, 0, 0, System.DateTimeKind.Utc),
            LatestKnownUpdateVersion = "2026.7.17",
            LatestKnownUpdateUrl = "https://github.com/vchelaru/FlatRedBall2/releases/tag/ae-Release_July_17_2026",
        };

        var json = JsonSerializer.Serialize(model);
        var restored = JsonSerializer.Deserialize<AppSettingsModel>(json);

        Assert.Equal(model.LastUpdateCheckUtc, restored!.LastUpdateCheckUtc);
        Assert.Equal(model.LatestKnownUpdateVersion, restored.LatestKnownUpdateVersion);
        Assert.Equal(model.LatestKnownUpdateUrl, restored.LatestKnownUpdateUrl);
    }
}
