using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>A file can hold NaN, or a number so large it reads as infinite (#2150). JSON has no such numbers, so they are its strings.</summary>
public class NonFiniteNumberTests : IDisposable
{
    private readonly CliWorkspace _w = new("sheet.png");

    public void Dispose() => _w.Dispose();

    // Every number a frame and its shapes hold, all the same.
    private string WriteAchx(string number)
    {
        string position = $"<X>{number}</X><Y>{number}</Y>";
        return _w.Write("odd.achx", $"""
            <AnimationChainArraySave><TimeMeasurementUnit>Second</TimeMeasurementUnit><CoordinateType>Pixel</CoordinateType><AnimationChain><Name>Odd</Name><Frame>
            <TextureName>sheet.png</TextureName><FrameLength>{number}</FrameLength>
            <LeftCoordinate>{number}</LeftCoordinate><RightCoordinate>0</RightCoordinate><TopCoordinate>{number}</TopCoordinate><BottomCoordinate>0</BottomCoordinate>
            <RelativeX>{number}</RelativeX><RelativeY>{number}</RelativeY>
            <ShapeCollectionSave>
            <AxisAlignedRectangleSaves><AxisAlignedRectangleSave><Name>R</Name>{position}<ScaleX>{number}</ScaleX><ScaleY>{number}</ScaleY></AxisAlignedRectangleSave></AxisAlignedRectangleSaves>
            <PolygonSaves><PolygonSave><Name>P</Name>{position}<Points><Point>{position}</Point></Points></PolygonSave></PolygonSaves>
            <CircleSaves><CircleSave><Name>C</Name>{position}<Radius>{number}</Radius></CircleSave></CircleSaves>
            </ShapeCollectionSave></Frame></AnimationChain></AnimationChainArraySave>
            """);
    }

    [Theory]
    [InlineData("NaN", "NaN")]
    [InlineData("1e39", "Infinity")]
    [InlineData("-1e39", "-Infinity")]
    public async Task DescribeJson_NonFiniteNumber_IsItsName(string stored, string name)
    {
        var r = await _w.RunAsync("describe", WriteAchx(stored), "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        var frame = doc.RootElement.GetProperty("chains")[0].GetProperty("frames")[0];
        frame.GetProperty("length").GetString().ShouldBe(name);
        frame.GetProperty("region").GetProperty("x").GetString().ShouldBe(name);
        frame.GetProperty("offset").GetProperty("y").GetString().ShouldBe(name);
        var shapes = frame.GetProperty("shapes");
        shapes[0].GetProperty("scaleX").GetString().ShouldBe(name);
        shapes[1].GetProperty("points")[0][0].GetString().ShouldBe(name);
        shapes[2].GetProperty("radius").GetString().ShouldBe(name);
    }

    [Theory]
    [InlineData("describe", "--json", "--summary")]
    [InlineData("describe", "--json")]
    [InlineData("at", "--json", "--chain", "Odd", "--time", "0")]
    public async Task Json_NonFiniteNumbers_IsValid(string command, params string[] options)
    {
        foreach (string number in new[] { "NaN", "1e39", "-1e39" })
        {
            var r = await _w.RunAsync([command, WriteAchx(number), .. options]);

            r.Err.ShouldBeEmpty();
            JsonDocument.Parse(r.Out).Dispose();
        }
    }

    [Theory]
    [InlineData("describe", "--json", "--summary")]
    [InlineData("describe", "--json")]
    public async Task Json_SequenceOfNonFiniteChain_IsValid(string command, params string[] options)
    {
        foreach (string number in new[] { "NaN", "1e39", "-1e39" })
        {
            WriteAchx(number);
            string path = _w.Write("odd.seqj", """
                {"version":1,"sources":["odd.achx"],"sequences":[{"name":"S","loop":true,"tracks":[{"name":"A","chains":[{"source":"odd.achx","chain":"Odd","start":0}]},{"name":"B","chains":[{"source":"odd.achx","chain":"Odd","start":1e308,"length":1.7e308}]}]}]}
                """);
            var r = await _w.RunAsync([command, path, .. options]);

            r.Err.ShouldBeEmpty();
            JsonDocument.Parse(r.Out).Dispose();
        }
    }
}
