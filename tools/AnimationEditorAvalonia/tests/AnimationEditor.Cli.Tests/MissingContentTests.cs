using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary><c>at</c> and <c>render</c> when what was asked for is not there: they say so instead of leaving it out (#1938).</summary>
public class MissingContentTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private static SequenceTrackSave Track(string name, params (string Chain, double Start)[] blocks) => new()
    {
        Name = name,
        Chains = blocks.Select(b => new SequenceChainReferenceSave { Source = "hero.achj", Chain = b.Chain, Start = b.Start }).ToList(),
    };

    private static SequenceTrackSave Plays(string sequence) =>
        new() { Name = "Play", References = [new SequenceReferenceSave { Sequence = sequence, Start = 0 }] };

    private static SequenceSave Sequence(string name, params SequenceTrackSave[] tracks) => new() { Name = name, Tracks = tracks.ToList() };

    private string Write(params SequenceSave[] sequences) => Write("hero.achj", sequences);

    private string Write(string source, SequenceSave[] sequences)
    {
        var path = _w.PathOf("test.seqj");
        new SequenceListSave { Sources = { source }, Sequences = sequences.ToList() }.Save(path);
        return path;
    }

    // Frame 0 of A has a texture that is on disk, frame 1 (from 0.5 s) uses badTexture.
    private string WriteTextureChains(string badTexture = "gone.png") => _w.Write("tex.achj", $$"""
        {"fileRelativeTextures":true,"timeMeasurementUnit":"Second","coordinateType":"Pixel","animationChains":[
          {"name":"A","frames":[
            {"textureName":"sheet.png","frameLength":0.5,"leftCoordinate":0,"rightCoordinate":16,"topCoordinate":0,"bottomCoordinate":16},
            {"textureName":"{{badTexture}}","frameLength":0.5,"leftCoordinate":0,"rightCoordinate":16,"topCoordinate":0,"bottomCoordinate":16}]},
          {"name":"NoTexture","frames":[{"frameLength":0.5}]}]}
        """);

    private Task<CliOutput> AtAsync(string file, string times, params string[] more) => _w.RunAsync(["at", file, "--time", times, .. more]);

    private Task<CliOutput> RenderAsync(string file, string times, params string[] more) =>
        _w.RunAsync(["render", file, "--at", times, "--out", _w.PathOf("out"), .. more]);

    private static (string Code, string Message) JsonError(CliOutput r)
    {
        r.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        var error = doc.RootElement.GetProperty("errors")[0];
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task MissingChain_FailsBothCommandsWithTheCheckMessage_InTextAndJson()
    {
        var file = Write(Sequence("Run", Track("Body", ("Walk", 0)), Track("Fx", ("Idel", 0))));
        const string message = "Run/Fx/0: source 'hero.achj' has no chain 'Idel'. Did you mean 'Idle'?";

        foreach (var r in new[] { await AtAsync(file, "0"), await RenderAsync(file, "0") })
        {
            r.Code.ShouldBe(1);
            r.Err.Trim().ShouldBe("aecli: " + message);
        }
        JsonError(await AtAsync(file, "0", "--json")).ShouldBe(("NotFound", message));
        JsonError(await RenderAsync(file, "0", "--json")).ShouldBe(("NotFound", message));
        Directory.Exists(_w.PathOf("out")).ShouldBeFalse();
    }

    [Fact]
    public async Task MissingChain_CountsForThatSequenceAndOnesThatPlayIt_NotForOthers()
    {
        var file = Write(Sequence("Run", Track("Body", ("Walk", 0))), Sequence("Bad", Track("Body", ("Gone", 0))), Sequence("Boss", Track("Body", ("Walk", 0)), Plays("Bad")));

        (await AtAsync(file, "0", "--sequence", "Run")).Code.ShouldBe(0);
        (await RenderAsync(file, "0", "--sequence", "Run")).Code.ShouldBe(0);
        (await AtAsync(file, "0", "--sequence", "Boss")).Err.ShouldContain("Bad/Body/0: source 'hero.achj' has no chain 'Gone'.");
        (await RenderAsync(file, "0", "--sequence", "Boss")).Err.ShouldContain("Bad/Body/0: source 'hero.achj' has no chain 'Gone'.");
    }

    // As for a missing source: the time asked for and the track's visibility do not matter.
    [Fact]
    public async Task MissingChain_OnAHiddenTrackOrAtAnotherTime_StillFails()
    {
        var hidden = Track("Fx", ("Gone", 5));
        hidden.Visible = false;
        var file = Write(Sequence("Run", Track("Body", ("Walk", 0)), hidden));

        (await AtAsync(file, "0")).Code.ShouldBe(1);
        (await RenderAsync(file, "0")).Code.ShouldBe(1);
    }

    [Fact]
    public async Task MissingPlayedSequence_AndUnlistedSource_FailAlike()
    {
        var unlisted = Track("Body");
        unlisted.Chains.Add(new SequenceChainReferenceSave { Source = "other.achj", Chain = "Walk" });
        var file = Write(Sequence("Boss", Track("Body", ("Walk", 0)), Plays("Rnu")), Sequence("Run", unlisted));

        JsonError(await AtAsync(file, "0", "--sequence", "Boss", "--json")).ShouldBe(("NotFound", "Track 'Boss/Play' plays 'Rnu', which does not exist."));
        JsonError(await RenderAsync(file, "0", "--sequence", "Boss", "--json")).ShouldBe(("NotFound", "Track 'Boss/Play' plays 'Rnu', which does not exist."));
        JsonError(await AtAsync(file, "0", "--sequence", "Run", "--json")).ShouldBe(("NotFound", "Run/Body/0 uses source 'other.achj', which is not in the sources list."));
    }

    // `at` gave every failure the code Unspecified; it now gives the codes of `render`.
    [Fact]
    public async Task UnknownSequence_HasTheSameCodeInBothCommands()
    {
        var file = Write(Sequence("Run", Track("Body", ("Walk", 0))));

        JsonError(await AtAsync(file, "0", "--sequence", "Rn", "--json")).ShouldBe(("NotFound", "No sequence named 'Rn'. Did you mean 'Run'?"));
        JsonError(await RenderAsync(file, "0", "--sequence", "Rn", "--json")).ShouldBe(("NotFound", "No sequence named 'Rn'. Did you mean 'Run'?"));
    }

    [Fact]
    public async Task Render_MissingTexture_FailsAndNamesIt_OnlyForTheFramesDrawn()
    {
        var file = WriteTextureChains();

        var mixed = await RenderAsync(file, "0,0.6", "--chain", "A");
        var json = await RenderAsync(file, "0.6", "--chain", "A", "--json");
        var fine = await _w.RunAsync("render", file, "--chain", "A", "--frame", "0", "--out", _w.PathOf("fine"));

        mixed.Code.ShouldBe(1);
        mixed.Err.Trim().ShouldBe("aecli: Texture 'gone.png' not found (frame A/1).");
        JsonError(json).ShouldBe(("TextureNotFound", "Texture 'gone.png' not found (frame A/1)."));
        Directory.Exists(_w.PathOf("out")).ShouldBeFalse();
        fine.Code.ShouldBe(0, fine.Err);
    }

    [Fact]
    public async Task Render_MissingTextureOfOneTrack_Fails_AndAtNeedsNoTextures()
    {
        WriteTextureChains();
        // At 0.6 Body shows A/1 (gone.png) while Fx shows A/0, which draws.
        var file = Write("tex.achj", [Sequence("Run",
            new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "tex.achj", Chain = "A" } } },
            new SequenceTrackSave { Name = "Fx", Chains = { new SequenceChainReferenceSave { Source = "tex.achj", Chain = "A", Start = 0.5 } } })]);

        (await RenderAsync(file, "0.6")).Err.Trim().ShouldBe("aecli: Texture 'gone.png' not found (frame A/1).");
        (await RenderAsync(file, "0")).Code.ShouldBe(0);
        (await AtAsync(file, "0.6")).Code.ShouldBe(0);
    }

    [Fact]
    public async Task Render_UnreadableTexture_FailsAndNamesIt()
    {
        _w.Write("bad.png", "not an image");

        var r = await RenderAsync(WriteTextureChains("bad.png"), "0,0.6", "--chain", "A", "--json");

        JsonError(r).ShouldBe(("TextureUndecodable", "Texture 'bad.png' could not be read as an image (frame A/1)."));
    }

    [Fact]
    public async Task Render_FramesWithoutATexture_SaysSo()
    {
        var r = await RenderAsync(WriteTextureChains(), "0", "--chain", "NoTexture");

        r.Code.ShouldBe(1);
        r.Err.Trim().ShouldBe("aecli: Nothing to draw: the frames shown have no texture.");
    }

    // Walk ends at 0.4 and Idle starts at 1, so nothing shows at 0.5 or 0.7.
    [Fact]
    public async Task Render_NothingVisible_SaysSoAtThoseTimes()
    {
        var file = Write(Sequence("Run", Track("Body", ("Walk", 0), ("Idle", 1))));

        var one = await RenderAsync(file, "0.5");
        one.Code.ShouldBe(1);
        one.Err.Trim().ShouldBe("aecli: Run at 0.5s: nothing visible");
        JsonError(await RenderAsync(file, "0.5,0.7", "--json")).ShouldBe(("Unspecified", "Run at 0.5s: nothing visible. Run at 0.7s: nothing visible"));
        (await AtAsync(file, "0.5")).Out.ShouldContain("(nothing visible)");
    }

    // Walk lasts 0.4 on paper; its float sum is a hair more, and 0.4 counts as the end.
    [Theory]
    [InlineData("0.4")]
    [InlineData("2.1")]
    public async Task PastTheEndOfALoopingSequence_BothCommandsSaySo_AndExitCodesStay(string time)
    {
        var file = Write(Sequence("Boss", Track("Body", ("Walk", 0))));
        var message = $"{time}s is past the end of Boss (0.4s); use --pass for later loops";

        var at = await AtAsync(file, time);
        var atJson = await AtAsync(file, "0," + time, "--json");
        var render = await RenderAsync(file, time);

        at.Code.ShouldBe(0, at.Err);
        at.Out.ShouldContain(message);
        at.Out.ShouldNotContain("(nothing visible)");
        atJson.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(atJson.Out);
        doc.RootElement.GetProperty("times")[0].TryGetProperty("message", out _).ShouldBeFalse();
        doc.RootElement.GetProperty("times")[1].GetProperty("message").GetString().ShouldBe(message);
        render.Code.ShouldBe(1);
        render.Err.Trim().ShouldBe($"aecli: {message}");
        (await RenderAsync(file, "0," + time)).Code.ShouldBe(0);
    }

    [Fact]
    public async Task Render_SomeTimesPastTheEnd_WritesThePngsAndSaysSo_InTextAndJson()
    {
        var file = Write(Sequence("Boss", Track("Body", ("Walk", 0))));
        const string message = "9s is past the end of Boss (0.4s); use --pass for later loops";

        var text = await RenderAsync(file, "0,9");
        var json = await RenderAsync(file, "0,9", "--json");

        text.Code.ShouldBe(0);
        text.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(p => Path.GetFileName(p.Trim())).ShouldBe(["Boss_0.png", "Boss_9.png"]);
        text.Err.Trim().ShouldBe("aecli: " + message);
        json.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(json.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("files").GetArrayLength().ShouldBe(2);
        doc.RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe([message]);
    }

    // Within the slack before the end the sprites are already hidden, so the time is past the end too.
    [Fact]
    public async Task PastTheEnd_IncludesTheBoundarySlack()
    {
        var file = Write(Sequence("Boss", Track("Body", ("Walk", 0))));

        (await AtAsync(file, "0.3999995")).Out.ShouldContain("is past the end of Boss (0.4s)");
        (await AtAsync(file, "0.39")).Out.ShouldContain("Walk frame 2");
    }

    [Fact]
    public async Task ReferenceCycle_FailsAsCheckSaysIt()
    {
        var file = Write(Sequence("A", Track("Body", ("Walk", 0)), Plays("B")), Sequence("B", Track("Body", ("Walk", 0)), Plays("A")), Sequence("C", Track("Body", ("Walk", 0))));

        foreach (var r in new[] { await AtAsync(file, "0", "--sequence", "A", "--json"), await RenderAsync(file, "0", "--sequence", "A", "--json") })
        {
            var (code, message) = JsonError(r);
            code.ShouldBe("WouldCycle");
            message.ShouldContain("Track 'A/Play' plays 'B', which plays 'A' back.");
        }
        (await AtAsync(file, "0", "--sequence", "C")).Code.ShouldBe(0);
    }

    [Fact]
    public async Task PastTheEnd_WithLoopOff_HoldsThePose_AndAnEmptySequenceIsJustEmpty()
    {
        var held = Sequence("Held", Track("Body", ("Walk", 0)));
        held.Loop = false;
        var file = Write(held, Sequence("Empty"));

        (await AtAsync(file, "2.1", "--sequence", "Held")).Out.ShouldContain("Walk frame 2");
        (await AtAsync(file, "2.1", "--sequence", "Empty")).Out.ShouldContain("(nothing visible)");
    }
}
