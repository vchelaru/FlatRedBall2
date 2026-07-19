using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

// #754 Phase A: BrowserIoManager is the browser-build counterpart to IoManager (IoManagerTests.cs)
// -- same .aeproperties companion-file contract, but backed by ICompanionFileStore (a
// FileSystemDirectoryHandle on the real browser build) instead of disk. FakeCompanionFileStore
// below stands in for that, same role FakeLocalStorage plays in BrowserSettingsStoreTests. The
// real store (AnimationEditor.Browser/NativeFolderCompanionFileStore.cs) is untestable JS-interop
// glue, same category as LocalStorageInterop.
public class BrowserIoManagerTests
{
    private sealed class FakeCompanionFileStore : ICompanionFileStore
    {
        public readonly Dictionary<string, string> Written = new();
        public Exception? ThrowOnWrite;

        public Task WriteAsync(string fileName, string contents)
        {
            if (ThrowOnWrite != null) throw ThrowOnWrite;
            Written[fileName] = contents;
            return Task.CompletedTask;
        }

        public Task<string?> TryReadAsync(string fileName) =>
            Task.FromResult(Written.TryGetValue(fileName, out var v) ? v : null);
    }

    private static (BrowserIoManager IoManager, FakeCompanionFileStore Store, IAppState AppState) Setup()
    {
        var applicationEvents = new ApplicationEvents();
        var selectedState = new SelectedState(new ProjectManager());
        var appState = new AppState(applicationEvents, selectedState);
        var store = new FakeCompanionFileStore();
        return (new BrowserIoManager(appState, store), store, appState);
    }

    // ── GetCompanionFileName ──────────────────────────────────────────────────

    [Fact]
    public void GetCompanionFileName_SwapsExtension_MatchingDesktopConvention()
    {
        var result = BrowserIoManager.GetCompanionFileName(new FilePath("hero.achx"));

        Assert.Equal("hero.aeproperties", result);
    }

    [Fact]
    public void GetCompanionFileName_GivenPathWithDirectory_DropsDirectory()
    {
        // Browser's FileSystemDirectoryHandle addresses files by bare name in one flat folder --
        // unlike desktop, which keeps the full directory in the companion path.
        var result = BrowserIoManager.GetCompanionFileName(new FilePath("sample/hero.achx"));

        Assert.Equal("hero.aeproperties", result);
    }

    // ── SaveCompanionFileFor ──────────────────────────────────────────────────

    [Fact]
    public void SaveCompanionFileFor_WritesToStoreUnderCompanionName()
    {
        var (ioManager, store, _) = Setup();

        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave());

        Assert.True(store.Written.ContainsKey("hero.aeproperties"));
    }

    [Fact]
    public void SaveCompanionFileFor_ContentRoundTripsThroughXmlFile()
    {
        var (ioManager, store, _) = Setup();
        var settings = new AESettingsSave { GridSize = 32, SnapToGrid = true };

        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), settings);

        var xml = store.Written["hero.aeproperties"];
        var deserialized = XmlFile.DeserializeFromString<AESettingsSave>(xml);
        Assert.Equal(32, deserialized.GridSize);
        Assert.True(deserialized.SnapToGrid);
    }

    [Fact]
    public void SaveCompanionFileFor_WhenStoreThrows_FiresSaveFailed()
    {
        var (ioManager, store, _) = Setup();
        store.ThrowOnWrite = new InvalidOperationException("permission denied");
        Exception? caught = null;
        ioManager.SaveFailed += (_, e) => caught = e;

        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave());

        Assert.Same(store.ThrowOnWrite, caught);
    }

    // ── LoadAndApplyCompanionFileFor ──────────────────────────────────────────

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileExists_SetsSnapToGridAndGridSize()
    {
        var (ioManager, _, appState) = Setup();
        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave { SnapToGrid = true, GridSize = 48 });

        ioManager.LoadAndApplyCompanionFileFor("hero.achx");

        Assert.True(appState.IsSnapToGridChecked);
        Assert.Equal(48, appState.GridSize);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileExists_FiresSettingsLoaded()
    {
        var (ioManager, _, _) = Setup();
        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave());
        var fired = false;
        ioManager.SettingsLoaded += _ => fired = true;

        ioManager.LoadAndApplyCompanionFileFor("hero.achx");

        Assert.True(fired);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenFileDoesNotExist_DoesNothing()
    {
        var (ioManager, _, appState) = Setup();
        appState.GridSize = 24; // baseline

        ioManager.LoadAndApplyCompanionFileFor("missing.achx");

        Assert.Equal(24, appState.GridSize);
    }

    [Fact]
    public void LoadAndApplyCompanionFileFor_WhenXmlIsInvalid_DoesNotThrow()
    {
        var (ioManager, store, _) = Setup();
        store.Written["bad.aeproperties"] = "<<NOT VALID XML>>";

        var ex = Record.Exception(() => ioManager.LoadAndApplyCompanionFileFor("bad.achx"));

        Assert.Null(ex);
    }

    // ── TryLoadCompanionSettings / TryLoadCompanionSettingsAsync ──────────────

    [Fact]
    public void TryLoadCompanionSettings_AlwaysReturnsNull_SynchronousReadNotSupported()
    {
        var (ioManager, _, _) = Setup();
        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave());

        // Unlike desktop's IoManager, the browser store is async-only -- documented limitation.
        Assert.Null(ioManager.TryLoadCompanionSettings("hero.achx"));
    }

    [Fact]
    public async Task TryLoadCompanionSettingsAsync_WhenFileExists_ReturnsSettings()
    {
        var (ioManager, _, _) = Setup();
        ioManager.SaveCompanionFileFor(new FilePath("hero.achx"), new AESettingsSave { GridSize = 64 });

        var result = await ioManager.TryLoadCompanionSettingsAsync("hero.achx");

        Assert.Equal(64, result?.GridSize);
    }

    // ── Recovery file (no-op on the browser build) ────────────────────────────

    [Fact]
    public void RecoveryFileExists_AlwaysReturnsFalse()
    {
        var (ioManager, _, _) = Setup();

        Assert.False(ioManager.RecoveryFileExists());
    }
}
