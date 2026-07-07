using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

// #610 follow-up: the browser build has no theme/zoom/grid UI yet to persist settings for
// (confirmed by grepping App.axaml.cs before writing this -- zero matches for Theme/Zoom/Grid).
// This adds the first one (a theme toggle) plus the storage layer to persist it, so the layer has
// a real consumer instead of being speculative plumbing. BrowserSettingsStore is the portable,
// testable persistence logic; the actual browser localStorage JS interop
// (AnimationEditor.Browser/LocalStorageInterop.cs) is untestable glue, same category as every
// other browser-only wiring in this codebase (see docs/BROWSER_FOLDER_WATCH_DECISION.md).
public class BrowserSettingsStoreTests
{
    private sealed class FakeLocalStorage : ILocalStorage
    {
        private readonly Dictionary<string, string> _values = new();
        public string? GetItem(string key) => _values.TryGetValue(key, out var v) ? v : null;
        public void SetItem(string key, string value) => _values[key] = value;
    }

    [Fact]
    public void LoadTheme_NothingStoredYet_ReturnsNull()
    {
        var store = new BrowserSettingsStore(new FakeLocalStorage());

        Assert.Null(store.LoadTheme());
    }

    [Fact]
    public void SaveTheme_ThenLoadTheme_RoundTrips()
    {
        var storage = new FakeLocalStorage();
        var store = new BrowserSettingsStore(storage);

        store.SaveTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, store.LoadTheme());
    }

    [Fact]
    public void SaveTheme_Light_ThenLoad_ReturnsLight()
    {
        var store = new BrowserSettingsStore(new FakeLocalStorage());

        store.SaveTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, store.LoadTheme());
    }

    [Fact]
    public void LoadTheme_CorruptStoredValue_ReturnsNullRatherThanThrowing()
    {
        var storage = new FakeLocalStorage();
        storage.SetItem("ae.theme", "not-a-real-theme-value");
        var store = new BrowserSettingsStore(storage);

        Assert.Null(store.LoadTheme());
    }
}
