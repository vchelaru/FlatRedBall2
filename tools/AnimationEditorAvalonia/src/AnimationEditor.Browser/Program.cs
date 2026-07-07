using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace AnimationEditor.Browser;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        // Fetch the bundled sample before Avalonia starts (M2: no local filesystem in the
        // browser, content only ever exists as in-memory bytes, fetched over HTTP).
        // HttpClient needs an absolute BaseAddress to resolve relative request URIs; main.js
        // passes the page URL as args[0] (runMain(mainAssemblyName, [location.href])).
        using var http = new HttpClient { BaseAddress = new Uri(args[0]) };
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

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
