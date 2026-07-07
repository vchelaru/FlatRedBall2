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
        await Task.WhenAll(achxTask, pngTask);
        SampleContent.AchxText = achxTask.Result;
        SampleContent.PngBytes = pngTask.Result;

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
