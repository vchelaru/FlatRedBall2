using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>describe --json exposes each chain's playback length in seconds, whatever the file's time unit.</summary>
public class ChainLengthTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    [Fact]
    public async Task Achx_ChainHasLengthInSeconds()
    {
        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");

        using var d = JsonDocument.Parse(r.Out);
        d.RootElement.GetProperty("chains")[0].GetProperty("length").GetDouble().ShouldBe(0.4, 1e-6);
    }

    [Fact]
    public async Task MillisecondFile_LengthIsStillSeconds()
    {
        _w.Write("ms.achj", """
            { "fileRelativeTextures": true, "timeMeasurementUnit": "Millisecond", "coordinateType": "Pixel",
              "animationChains": [ { "name": "Walk", "frames": [
                { "textureName": "sheet.png", "frameLength": 100, "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32 },
                { "textureName": "sheet.png", "frameLength": 300, "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32 } ] } ] }
            """);

        var r = await _w.RunAsync("describe", _w.PathOf("ms.achj"), "--json");

        using var d = JsonDocument.Parse(r.Out);
        d.RootElement.GetProperty("chains")[0].GetProperty("length").GetDouble().ShouldBe(0.4, 1e-6);
    }

    [Fact]
    public async Task Seqj_BlockHasChainLength()
    {
        _w.Copy("hero.achj");
        _w.Write("a.seqj", """
            { "version": 1, "sources": ["hero.achx"],
              "sequences": [ { "name": "Run", "loop": true, "tracks": [
                { "name": "Body", "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 0, "length": 0.8 } ] } ] } ] }
            """);

        var r = await _w.RunAsync("describe", _w.PathOf("a.seqj"), "--json");

        r.Code.ShouldBe(0, r.Err);
        using var d = JsonDocument.Parse(r.Out);
        var block = d.RootElement.GetProperty("sequences")[0].GetProperty("tracks")[0].GetProperty("blocks")[0];
        block.GetProperty("chainLength").GetDouble().ShouldBe(0.4, 1e-6);
        block.GetProperty("end").GetDouble().ShouldBe(0.8, 1e-6);
    }
}
