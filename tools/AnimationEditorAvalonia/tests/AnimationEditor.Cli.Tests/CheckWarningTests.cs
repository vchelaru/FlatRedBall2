using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Warnings for bad regions, empty chains, zero-length frames and texture case, and the ones an edit introduces.</summary>
public class CheckWarningTests : IDisposable
{
    private const string EmptyChain = "  <AnimationChain><Name>Nothing</Name></AnimationChain>\n</AnimationChainArraySave>";

    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string Hero(string from, string to)
    {
        var path = _w.PathOf("hero.achx");
        var text = File.ReadAllText(path);
        text.ShouldContain(from);
        File.WriteAllText(path, text.Replace(from, to));
        return path;
    }

    private async Task<List<(string Code, string Message)>> Warnings(string path)
    {
        var r = await _w.RunAsync("check", path, "--json");
        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        return doc.RootElement.GetProperty("issues").EnumerateArray()
            .Select(i => (i.GetProperty("code").GetString()!, i.GetProperty("message").GetString()!)).ToList();
    }

    private async Task<List<string>> EditWarnings(string path, params string[] ops)
    {
        var r = await _w.RunAsync(["edit", path, "--json", .. ops.SelectMany(o => new[] { "--op", o })]);
        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("changed").GetArrayLength().ShouldBeGreaterThan(0);
        return doc.RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToList();
    }

    [Fact]
    public async Task Check_CleanFile_HasNoWarnings() => (await Warnings(_w.PathOf("hero.achx"))).ShouldBeEmpty();

    [Theory]
    [InlineData("<RightCoordinate>48", "<RightCoordinate>99999", "32,0,99967,32")]
    [InlineData("<LeftCoordinate>32", "<LeftCoordinate>-4", "-4,0,52,32")]
    [InlineData("<RightCoordinate>48", "<RightCoordinate>32", "32,0,0,32")]
    public async Task Check_PixelRegionNotInsideItsTexture_Warns(string from, string to, string region)
    {
        var (code, message) = (await Warnings(Hero(from, to))).ShouldHaveSingleItem();

        code.ShouldBe("region-outside-texture");
        message.ShouldContain("Walk/2");
        message.ShouldContain(region);
        message.ShouldContain("48x32");
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("1.5", 1)]
    public async Task Check_UvRegion_IsComparedInUv(string right, int expected)
    {
        var path = Hero("<RightCoordinate>48", $"<RightCoordinate>{right}");
        // Every other edge of the fixture becomes a fraction of the 48x32 sheet.
        var text = File.ReadAllText(path).Replace("<CoordinateType>Pixel", "<CoordinateType>UV")
            .Replace("Coordinate>16<", "Coordinate>0.3333333<").Replace("Coordinate>32<", "Coordinate>0.6666667<");
        File.WriteAllText(path, text);

        var regions = (await Warnings(path)).Where(w => w.Code == "region-outside-texture").ToList();

        regions.Count.ShouldBe(expected);
        if (expected > 0) regions[0].Message.ShouldContain("32,0,40,21");
    }

    [Fact]
    public async Task Check_UnreadableTexture_SkipsItsRegions()
    {
        File.WriteAllText(_w.PathOf("sheet.png"), "not a png");

        (await Warnings(Hero("<RightCoordinate>48", "<RightCoordinate>99999"))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_ChainWithNoFrames_Warns()
    {
        var (code, message) = (await Warnings(Hero("</AnimationChainArraySave>", EmptyChain))).ShouldHaveSingleItem();

        code.ShouldBe("empty-chain");
        message.ShouldContain("'Nothing'");
    }

    [Fact]
    public async Task Check_ZeroLengthFrame_WarnsOnlyInAChainWithSeveralFrames()
    {
        Hero("<FrameLength>0.2", "<FrameLength>0"); // Walk/2, of three
        var (code, message) = (await Warnings(Hero("<FrameLength>0.5", "<FrameLength>0"))).ShouldHaveSingleItem(); // Idle/0, alone

        code.ShouldBe("zero-length-frame");
        message.ShouldContain("Walk/2");
    }

    [Theory]
    [InlineData("Sheet.png", "'sheet.png'")]
    [InlineData("art/sheet.png", "'Art/sheet.png'")]
    public async Task Check_TextureNameInAnotherCase_Warns(string name, string onDisk)
    {
        Directory.CreateDirectory(_w.PathOf("Art"));
        File.Copy(_w.PathOf("sheet.png"), Path.Combine(_w.PathOf("Art"), "sheet.png"));
        var path = Hero("<TextureName>sheet.png", $"<TextureName>{name}");

        // Where file names are case-sensitive the texture is simply missing, which is the error it already was.
        if (!File.Exists(_w.PathOf(name)))
        {
            (await _w.RunAsync("check", path)).Out.ShouldContain("missing-texture");
            return;
        }
        var (code, message) = (await Warnings(path)).ShouldHaveSingleItem();

        code.ShouldBe("texture-case");
        message.ShouldContain($"'{name}'", Case.Sensitive);
        message.ShouldContain(onDisk, Case.Sensitive);
    }

    [Fact]
    public async Task Check_TextureNameThroughParentFolder_InTheRightCase_HasNoWarning()
    {
        var folder = Path.GetFileName(_w.Dir);

        (await Warnings(Hero("<TextureName>sheet.png", $"<TextureName>../{folder}/./sheet.png"))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_IntroducedWarning_IsPrintedInTextAndJson()
    {
        var text = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", "add-chain Run");
        var json = await EditWarnings(_w.PathOf("hero.achj"), "add-chain Run");

        text.Code.ShouldBe(0);
        text.Out.ShouldContain("warning: The edit introduced a check warning [empty-chain]: Chain 'Run'");
        json.ShouldHaveSingleItem().ShouldContain("[empty-chain]: Chain 'Run'");
    }

    [Fact]
    public async Task Edit_FileWithWarnings_SavesAndReportsOnlyNewOnes()
    {
        Hero("</AnimationChainArraySave>", EmptyChain);
        var path = Hero("<RightCoordinate>48", "<RightCoordinate>99999"); // Walk/2

        // Renaming the chains and shifting the bad frame moves the old warnings, it does not add any.
        (await EditWarnings(path, "rename-chain Walk to=Stride", "rename-chain Nothing to=Void", "delete-frame Stride/0")).ShouldBeEmpty();
        // A copy of the bad frame (held in UV during the edit) is a second bad frame.
        var added = await EditWarnings(path, "duplicate-frame Stride/1");

        added.ShouldHaveSingleItem().ShouldContain("[region-outside-texture]");
        added[0].ShouldContain("32,0,99967,32");
    }

    [Fact]
    public async Task EditSequence_IntroducedWarning_IsPrintedInTextAndJson()
    {
        var path = WriteSeq();

        var json = await EditWarnings(path, "set-block-start Run/Body/1 start=0.5");
        var text = await _w.RunAsync("edit", path, "--op", "add-block Run/Body source=hero.achj chain=Idle start=0.6");

        json.ShouldHaveSingleItem().ShouldContain("[cut-short]: Run/Body/0");
        text.Code.ShouldBe(0, text.Out);
        text.Out.ShouldContain("warning: The edit introduced a check warning [cut-short]: Run/Body/1");
        text.Out.ShouldNotContain("Run/Body/0"); // already cut short by the first edit
    }

    [Fact]
    public async Task EditSequence_Renames_DoNotRepeatOldWarnings()
    {
        var path = WriteSeq();
        await EditWarnings(path, "set-block-start Run/Body/1 start=0.5");

        (await EditWarnings(path, "rename-sequence Run to=Sprint", "rename-track Sprint/Body to=Torso")).ShouldBeEmpty();
    }

    [Fact]
    public async Task EditSequence_EveryWarningKind_SurvivesRenamesAndReorders()
    {
        File.Delete(_w.PathOf("sheet.png"));
        var path = WriteSeq(s =>
        {
            var blocks = s.Sequences[0].Tracks[0].Chains;
            blocks[1].Start = 0.5; // cuts the first short
            blocks.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0.5 }); // the second never plays
            var once = new SequenceSave { Name = "Long", Loop = false };
            once.Tracks.Add(new SequenceTrackSave { Name = "Clip", Continuous = true, Reference = new SequenceReferenceSave { Sequence = "Run" } });
            once.Tracks.Add(new SequenceTrackSave { Name = "Other" });
            s.Sequences.Add(once);
        });
        (await Warnings(path)).Select(w => w.Code).ShouldBe(["missing-texture", "cut-short", "unreachable-block", "continuous-ignored"]);

        (await EditWarnings(path,
            "rename-track Long/Clip to=Legs", "rename-track Run/Body to=Torso", "rename-sequence Run to=Sprint", "rename-sequence Long to=Wide",
            "reorder-track Wide/Legs index=1", "reorder-sequence Wide index=0", "add-track Sprint name=Front")).ShouldBeEmpty();
        (await Warnings(path)).Count.ShouldBe(4);
    }

    [Fact]
    public async Task EditSequence_OneWarningFixedAndAnotherCaused_ReportsTheNewOne()
    {
        var path = WriteSeq(s =>
        {
            s.Sequences[0].Tracks[0].Chains[1].Start = 0.5;
            var other = new SequenceTrackSave { Name = "Other" };
            other.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 0.8 });
            other.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 1 });
            s.Sequences[0].Tracks.Add(other);
        });

        var added = await EditWarnings(path, "set-block-start Run/Body/1 start=1", "set-block-start Run/Other/1 start=0.5");

        added.ShouldHaveSingleItem().ShouldContain("[cut-short]: Run/Other/0");
    }

    [Fact]
    public async Task EditSequence_CopyOfAWarnedTrack_IsReportedAndTheFileGainsNothingElse()
    {
        var path = WriteSeq(s => s.Sequences[0].Tracks[0].Chains[1].Start = 0.5);

        var added = await EditWarnings(path, "duplicate-track Run/Body to=Run");

        added.ShouldHaveSingleItem().ShouldContain("[cut-short]: Run/Body 2/0");
        // Built without any edit, so whatever the edit adds to the objects it tracks would show.
        var saved = SequenceListSave.FromFile(path);
        saved.Sequences[0].UnknownProperties.ShouldBeEmpty();
        saved.Sequences[0].Tracks.Count.ShouldBe(2);
        foreach (var track in saved.Sequences[0].Tracks)
        {
            track.UnknownProperties.ShouldBeEmpty();
            foreach (var block in track.Chains) block.UnknownProperties.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Edit_TextureChangeThatLeavesARegionOutside_IsReported()
    {
        _w.Copy("other.png"); // 48x32
        var bytes = File.ReadAllBytes(_w.PathOf("other.png"));
        bytes[19] = 16; // 16 pixels wide
        File.WriteAllBytes(_w.PathOf("other.png"), bytes);

        // Walk/2 has no texture yet, so its 32,0,16,32 region could not be compared with anything.
        var path = _w.PathOf("hero.achx");
        var text = File.ReadAllText(path);
        var at = text.IndexOf("<FrameLength>0.2", StringComparison.Ordinal);
        var name = text.LastIndexOf("sheet.png", at, StringComparison.Ordinal);
        File.WriteAllText(path, text.Remove(name, "sheet.png".Length));
        (await Warnings(path)).ShouldBeEmpty();

        var added = await EditWarnings(path, "set-frame-texture Walk/2 texture=other.png");

        added.ShouldHaveSingleItem().ShouldContain("[region-outside-texture]: Frame Walk/2");
        added[0].ShouldContain("16x32 texture 'other.png'");
    }

    [Fact]
    public async Task CheckSequence_SourceWithMissingTexture_Warns()
    {
        var path = WriteSeq();
        File.Delete(_w.PathOf("sheet.png"));

        var (code, message) = (await Warnings(path)).ShouldHaveSingleItem();

        code.ShouldBe("missing-texture");
        message.ShouldContain("hero.achj");
        message.ShouldContain("sheet.png");
    }

    private string WriteSeq(Action<SequenceListSave>? change = null)
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 0.8 });
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 1 });
        var run = new SequenceSave { Name = "Run" };
        run.Tracks.Add(body);
        save.Sequences.Add(run);
        change?.Invoke(save);
        var path = _w.PathOf("hero.seqj");
        save.Save(path);
        return path;
    }
}
