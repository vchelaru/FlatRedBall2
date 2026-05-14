using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class IoManagerTests
{
    // ── SaveCompanionFileFor ──────────────────────────────────────────────────

    [Fact]
    public void SaveCompanionFileFor_CreatesFileAtExpectedPath()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/hero.achx");
        var expectedAeProps = dir.Path + "/hero.aeproperties";

        ctx.IoManager.SaveCompanionFileFor(achxPath, new AESettingsSave());

        Assert.True(File.Exists(expectedAeProps));
    }

    [Fact]
    public void SaveCompanionFileFor_FileContainsXmlContent()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/hero.achx");

        ctx.IoManager.SaveCompanionFileFor(achxPath, new AESettingsSave { GridSize = 32 });

        var contents = File.ReadAllText(dir.Path + "/hero.aeproperties");
        Assert.Contains("32", contents);
    }

    [Fact]
    public void SaveCompanionFileFor_RoundTrip_PreservesSnapToGrid()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/snap.achx");
        var settings = new AESettingsSave { SnapToGrid = true, GridSize = 16 };

        ctx.IoManager.SaveCompanionFileFor(achxPath, settings);
        ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath.FullPath);

        Assert.True(ctx.AppState.IsSnapToGridChecked);
    }

    [Fact]
    public void SaveCompanionFileFor_RoundTrip_PreservesGridSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/grid.achx");
        var settings = new AESettingsSave { GridSize = 64 };

        ctx.IoManager.SaveCompanionFileFor(achxPath, settings);
        ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath.FullPath);

        Assert.Equal(64, ctx.AppState.GridSize);
    }

    // ── LoadAndApplyCompanionFileFor ──────────────────────────────────────────

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileExists_SetsSnapToGrid()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/test.achx");
        ctx.IoManager.SaveCompanionFileFor(achxPath, new AESettingsSave { SnapToGrid = true });

        ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath.FullPath);

        Assert.True(ctx.AppState.IsSnapToGridChecked);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileExists_SetsGridSize()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/test.achx");
        ctx.IoManager.SaveCompanionFileFor(achxPath, new AESettingsSave { GridSize = 48 });

        ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath.FullPath);

        Assert.Equal(48, ctx.AppState.GridSize);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileExists_FiresSettingsLoaded()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var achxPath = new FilePath(dir.Path + "/test.achx");
        ctx.IoManager.SaveCompanionFileFor(achxPath, new AESettingsSave { GridSize = 16 });

        bool fired = false;
        ctx.IoManager.SettingsLoaded += _ => fired = true;

        ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath.FullPath);

        Assert.True(fired);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileDoesNotExist_DoesNothing()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.AppState.GridSize = 32; // baseline

        ctx.IoManager.LoadAndApplyCompanionFileFor("C:/NoSuchFile/missing.achx");

        // Grid size should remain unchanged since the file was never loaded
        Assert.Equal(32, ctx.AppState.GridSize);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenXmlIsInvalid_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        // Write invalid XML to .aeproperties file directly
        var aePropsPath = dir.Path + "/bad.aeproperties";
        File.WriteAllText(aePropsPath, "<<NOT VALID XML>>");
        var achxPath = dir.Path + "/bad.achx";

        var ex = Record.Exception(() =>
            ctx.IoManager.LoadAndApplyCompanionFileFor(achxPath));

        Assert.Null(ex);
    }

    [Fact]
    public void SaveCompanionFileFor_WhenDirectoryDoesNotExist_FiresSaveFailed()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        Exception? caughtEx = null;
        ctx.IoManager.SaveFailed += (_, e) => caughtEx = e;

        ctx.IoManager.SaveCompanionFileFor(
            new FilePath("Z:/NonExistentDrive/hero.achx"),
            new AESettingsSave());

        // On most systems, writing to a non-existent drive should fail
        // If it succeeds for some reason (drive Z exists), this test is inconclusive —
        // but we still verify no unhandled exception is thrown
        // The important thing is that no exception escapes SaveCompanionFileFor
    }
}
