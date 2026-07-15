using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace AnimationEditor.Browser;

internal sealed class Program
{
    /// <summary>
    /// The page's own URL, captured from <c>args[0]</c> (see the comment below). Phase 11
    /// (#654)'s "Open in New Browser Tab" tab-context-menu item reopens this same URL via
    /// <see cref="Avalonia.Platform.Storage.ILauncher.LaunchUriAsync"/> -- a fresh instance, not
    /// true state transfer, since there's no server-side session to hand off.
    /// </summary>
    public static string PageUrl { get; private set; } = "";

    private static async Task Main(string[] args)
    {
        // Fetch the bundled sample before Avalonia starts (M2: no local filesystem in the
        // browser, content only ever exists as in-memory bytes, fetched over HTTP).
        // HttpClient needs an absolute BaseAddress to resolve relative request URIs; main.js
        // passes the page URL as args[0] (runMain(mainAssemblyName, [location.href])).
        PageUrl = args[0];
        // WasmAppHost may pass location.href with ?arg=... query noise; BaseAddress must be
        // origin+path only or relative sample fetches can fail and hang startup on the spinner.
        var pageUri = new Uri(args[0]);
        var contentBase = new UriBuilder(pageUri) { Query = "", Fragment = "" }.Uri;
        using var http = new HttpClient { BaseAddress = contentBase };
        var achxTask = http.GetStringAsync("sample/player.achx");
        var pngTask = http.GetByteArrayAsync("sample/player.png");
        // #610: the localStorage JS module must be imported before any BrowserSettingsStore call
        // (App.axaml.cs's BuildView reads the persisted theme synchronously on startup) --
        // independent of the sample fetches above, so it rides along in the same WhenAll.
        var storageInitTask = LocalStorageInterop.InitializeAsync();
        // #622 (Phase 5): same requirement for the PixiJS export button's Blob-download path.
        var downloadInitTask = DownloadInterop.InitializeAsync();
        await Task.WhenAll(achxTask, pngTask, storageInitTask, downloadInitTask);
        SampleContent.AchxText = achxTask.Result;
        SampleContent.PngBytes = pngTask.Result;

        await Task.WhenAll(
            StoragePermissionInterop.InitializeAsync(),
            NativeFolderInterop.InitializeAsync());

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
