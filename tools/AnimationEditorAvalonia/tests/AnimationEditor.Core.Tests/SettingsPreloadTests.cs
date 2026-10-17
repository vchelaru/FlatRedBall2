using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

public class SettingsPreloadTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ae-settings-preload-" + Guid.NewGuid().ToString("N"));

    public SettingsPreloadTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private FilePath WriteSettings(AppSettingsModel settings, string name = "AESettings.json")
    {
        var path = new FilePath(Path.Combine(_dir, name));
        File.WriteAllText(path.FullPath, JsonSerializer.Serialize(settings));
        return path;
    }

    [Fact]
    public void Take_ForTheStartedPath_ReturnsWhatWasOnDisk()
    {
        var path = WriteSettings(new AppSettingsModel { SidebarWidth = 321.0 });

        var preload = new SettingsPreload(path);

        Assert.Equal(321.0, preload.Take(path)!.SidebarWidth);
    }

    [Fact]
    public async Task Take_ForAnotherPath_ReturnsNullSoTheCallerReadsItItself()
    {
        var started = WriteSettings(new AppSettingsModel { SidebarWidth = 321.0 });
        var other = WriteSettings(new AppSettingsModel { SidebarWidth = 99.0 }, "Other.json");

        var preload = new SettingsPreload(started);

        Assert.Null(preload.Take(other));
        await preload.Completion; // let the background read finish before the temp dir goes
    }

    [Fact]
    public void Take_Twice_HandsOverOnlyOnce()
    {
        // A second window must re-read the file: the preload is a startup snapshot and would
        // otherwise hand it a stale copy of settings the first window has since saved.
        var path = WriteSettings(new AppSettingsModel { SidebarWidth = 321.0 });
        var preload = new SettingsPreload(path);

        Assert.NotNull(preload.Take(path));
        Assert.Null(preload.Take(path));
    }

    [Fact]
    public void Take_WhenFileIsMissing_ReturnsDefaults()
    {
        var path = new FilePath(Path.Combine(_dir, "missing.json"));

        var settings = new SettingsPreload(path).Take(path)!;

        Assert.Empty(settings.OpenTabPaths);
        Assert.Equal(new AppSettingsModel().SidebarWidth, settings.SidebarWidth);
    }

    [Fact]
    public void Take_WhenFileIsCorrupt_ReturnsDefaults()
    {
        var path = new FilePath(Path.Combine(_dir, "AESettings.json"));
        File.WriteAllText(path.FullPath, "{ not json");

        var settings = new SettingsPreload(path).Take(path)!;

        Assert.Empty(settings.OpenTabPaths);
    }
}
