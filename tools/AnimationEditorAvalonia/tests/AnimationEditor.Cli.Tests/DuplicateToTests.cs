using System.Text.Json;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using AnimationEditor.Editing;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Runs inline ops (one batch) on a fixture copy and reads the result back.</summary>
public abstract class InlineBatchTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "tiles.tsx", "sheet.png", "other.png");

    public void Dispose() => _w.Dispose();

    protected string PathOf(string file) => _w.PathOf(file);

    protected void Write(string file, string text) => _w.Write(file, text);

    protected string Raw(string file) => File.ReadAllText(_w.PathOf(file));

    /// <summary>The batch succeeds; returns what it reports as changed.</summary>
    protected async Task<string[]> Ok(string file, params string[] ops)
    {
        var r = await Edit(file, ops);
        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        return doc.RootElement.GetProperty("changed").EnumerateArray().Select(c => c.GetString()!).ToArray();
    }

    /// <summary>The batch fails with <paramref name="code"/> and the file is untouched; returns the error message.</summary>
    protected async Task<string> Fails(string file, string code, params string[] ops)
    {
        var before = _w.Bytes(file);
        var r = await Edit(file, ops);
        r.Code.ShouldBe(1, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(before);
        using var doc = JsonDocument.Parse(r.Out);
        var error = doc.RootElement.GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe(code, r.Out);
        return error.GetProperty("message").GetString()!;
    }

    private Task<CliOutput> Edit(string file, string[] ops) =>
        _w.RunAsync(["edit", _w.PathOf(file), "--json", .. ops.SelectMany(o => new[] { "--op", o })]);

    protected List<AnimationChainSave> Chains(string file) => AnimationFileReader.Read(_w.PathOf(file)).Document!.AnimationChains;

    protected List<AnimationFrameSave> Frames(string file, string chain) => Chains(file).Single(c => c.Name == chain).Frames;

    protected static string[] Events(AnimationFrameSave frame) => frame.Events.Select(e => e.Name).ToArray();
}

public class DuplicateToTests : InlineBatchTests
{
    /// <summary>Left edges of the chain's regions; the fixture's Walk is 0, 16, 32 and Idle is 0.</summary>
    private float[] Lefts(string chain) => Frames("hero.achx", chain).Select(f => f.LeftCoordinate).ToArray();

    private static string[] Rects(AnimationFrameSave frame) => frame.ShapesSave?.AARectSaves.Select(s => s.Name).ToArray() ?? [];

    // ---- duplicate-frame ----

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task DuplicateFrame_ToChain_KeepsEveryFieldOfTheFrame(string file)
    {
        await Ok(file,
            "set-frame-texture Walk/2 texture=other.png",
            "set-frame-flip Walk/2 flipV=true",
            "set-frame-offset Walk/2 x=3 y=-4",
            "set-frame-color Walk/2 red=10 alpha=40 operation=add",
            "set-frame-keyframe-curve Walk/2 group=color curve=ease-in-out",
            "set-frame-keyframe Walk/2 group=offset value=false",
            "set-shape-keyframe-curve Walk/2/rect/Hitbox group=offset curve=linear",
            "add-frame-event Walk/2 name=Step data=left",
            "duplicate-frame Walk/2 to=Idle");

        var source = Frames(file, "Walk")[2];
        var idle = Frames(file, "Idle");
        idle.Count.ShouldBe(2);
        var copy = idle[1];
        (copy.LeftCoordinate, copy.TopCoordinate, copy.RightCoordinate, copy.BottomCoordinate).ShouldBe(
            (source.LeftCoordinate, source.TopCoordinate, source.RightCoordinate, source.BottomCoordinate));
        copy.TextureName.ShouldBe("other.png");
        copy.FrameLength.ShouldBe(0.2f);
        copy.FlipVertical.ShouldBeTrue();
        (copy.RelativeX, copy.RelativeY).ShouldBe((3f, -4f));
        (copy.Red, copy.Alpha, copy.ColorOperation).ShouldBe((10, 40, ColorOperation.Add));
        (copy.ColorInterpolation, copy.ColorEasing).ShouldBe(("Quadratic", "InOut"));
        copy.SkipOffsetKeyframe.ShouldBeTrue();
        copy.ShapesSave!.AARectSaves.Single().OffsetInterpolation.ShouldBe("Linear");
        copy.ShapesSave!.CircleSaves.Single().Name.ShouldBe("Foot");
        copy.Events.Select(e => (e.Name, e.Data)).ShouldBe([("Step", "left")]);
    }

    [Fact]
    public async Task DuplicateFrame_ToAndIndex_InsertsTheSelectionAsABlockInOrder()
    {
        var changed = await Ok("hero.achx", "duplicate-frame Walk/1,2 to=Idle index=0");

        Lefts("Idle").ShouldBe([16f, 32f, 0f]);
        changed.ShouldBe(["Idle/0", "Idle/1"]);
    }

    [Fact]
    public async Task DuplicateFrame_ToOwnChain_ReadsTheSelectionBeforeInserting()
    {
        await Ok("hero.achx", "duplicate-frame Walk/* to=Walk index=1");

        Lefts("Walk").ShouldBe([0f, 0f, 16f, 32f, 16f, 32f]);
    }

    [Fact]
    public async Task DuplicateFrame_IndexAlone_StaysInEachFramesOwnChain()
    {
        await Ok("hero.achx", "set-frame-region Idle/0 region=16,0,16,32", "duplicate-frame */0 index=1", "duplicate-frame Walk/3 index=0");

        Lefts("Walk").ShouldBe([32f, 0f, 0f, 16f, 32f]);
        Lefts("Idle").ShouldBe([16f, 16f]);
    }

    [Fact]
    public async Task DuplicateFrame_WithoutToOrIndex_LandsRightAfterEachSource()
    {
        await Ok("hero.achx", "duplicate-frame Walk/0-1");

        Lefts("Walk").ShouldBe([0f, 0f, 16f, 16f, 32f]);
    }

    [Fact]
    public async Task DuplicateFrame_ThenDelete_MovesTheFrame()
    {
        await Ok("hero.achx", "duplicate-frame Walk/1 to=Idle", "delete-frame Walk/1");

        Lefts("Walk").ShouldBe([0f, 32f]);
        Lefts("Idle").ShouldBe([0f, 16f]);
    }

    [Theory]
    [InlineData("Walk/0 to=Idel", "NotFound")]
    [InlineData("Walk/0 to=Idl*", "NotFound")]
    [InlineData("Walk/0 to=Idle index=2", "IndexOutOfRange")]
    [InlineData("Walk/0 to=Idle index=-1", "IndexOutOfRange")]
    [InlineData("Walk/3 to=Idle", "IndexOutOfRange")]
    [InlineData("Nope/0 to=Idle", "NotFound")]
    public async Task DuplicateFrame_BadSourceOrTarget_WritesNothing(string op, string code) =>
        await Fails("hero.achx", code, "duplicate-frame " + op);

    [Fact]
    public async Task DuplicateFrame_LockedTargetIsRefused_LockedSourceIsCopied()
    {
        await Fails("hero.achx", "Locked", "set-chain-locked Idle value=true", "duplicate-frame Walk/1 to=Idle");
        await Fails("hero.achx", "Locked", "set-chain-locked Idle value=true", "duplicate-frame Idle/0 index=0");
        await Ok("hero.achx", "set-chain-locked Idle value=true", "duplicate-frame Idle/0 to=Walk");

        Frames("hero.achx", "Walk").Count.ShouldBe(4);
    }

    [Fact]
    public async Task DuplicateFrame_To_WorksOnATsx()
    {
        await Ok("tiles.tsx", "duplicate-frame Walk/1-2 to=ID:5");

        Raw("tiles.tsx").ReplaceLineEndings("\n").ShouldContain(
            "<frame tileid=\"5\" duration=\"150\" />\n   <frame tileid=\"1\" duration=\"100\" />\n   <frame tileid=\"2\" duration=\"200\" />");
    }

    // ---- duplicate-shape ----

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task DuplicateShape_ToFrames_CopiesValuesAndKeyframesUnderTheSameName(string file)
    {
        await Ok(file,
            "set-shape-keyframe-curve Walk/2/rect/Hitbox group=size curve=linear",
            "duplicate-shape Walk/2/rect/Hitbox to=Walk/0-1",
            "duplicate-shape Walk/2/rect/Hitbox to=I*/*");

        foreach (var frame in Frames(file, "Walk").Take(2).Concat(Frames(file, "Idle")))
        {
            var copy = frame.ShapesSave!.AARectSaves.Single();
            (copy.Name, copy.X, copy.Y, copy.ScaleX, copy.ScaleY, copy.SizeInterpolation).ShouldBe(("Hitbox", 1f, 2f, 4f, 6f, "Linear"));
        }
        Rects(Frames(file, "Walk")[2]).ShouldBe(["Hitbox"]);
    }

    [Fact]
    public async Task DuplicateShape_SelectorSourceToOneFrame_CopiesEachAndNamesThem()
    {
        var changed = await Ok("hero.achx",
            "add-shape Walk/2 type=rect name=Hurt",
            "duplicate-shape Walk/2/rect/* to=Idle/0");

        Rects(Frames("hero.achx", "Idle")[0]).ShouldBe(["Hitbox", "Hurt"]);
        changed.Where(c => c.StartsWith("Idle")).ShouldBe(["Idle/0/rect/Hitbox", "Idle/0/rect/Hurt"]);
    }

    [Fact]
    public async Task DuplicateShape_NameTakenInATarget_FailsTheBatch()
    {
        // Only the last target has the name already, and a shape of another type holding it clashes too.
        (await Fails("hero.achx", "DuplicateName", "duplicate-shape Walk/2/rect/Hitbox to=Walk/*")).ShouldContain("Walk/2");
        await Fails("hero.achx", "DuplicateName",
            "add-shape Idle/0 type=circle name=Hitbox", "duplicate-shape Walk/2/rect/Hitbox to=Idle/0");
        await Fails("hero.achx", "DuplicateName",
            "duplicate-shape Walk/2/rect/Hitbox to=Walk/0", "duplicate-shape Walk/*/rect/Hitbox to=Idle/0");
    }

    [Fact]
    public async Task DuplicateShape_WithoutTo_StaysInItsFrameUnderANumberedName()
    {
        await Ok("hero.achx", "duplicate-shape Walk/2/rect/Hitbox");

        Rects(Frames("hero.achx", "Walk")[2]).ShouldBe(["Hitbox", "Hitbox2"]);
    }

    [Theory]
    [InlineData("Nope*/0", "NoMatch")]
    [InlineData("Nope/0", "NotFound")]
    [InlineData("Walk/0-3", "IndexOutOfRange")]
    [InlineData("Walk", "InvalidAddress")]
    [InlineData("Walk*", "InvalidAddress")]
    public async Task DuplicateShapeAndEvent_BadTarget_WritesNothing(string to, string code)
    {
        await Fails("hero.achx", code, $"duplicate-shape Walk/2/rect/Hitbox to={to}");
        await Fails("hero.achx", code, "add-frame-event Walk/0 name=Step", $"duplicate-frame-event Walk/0/event/0 to={to}");
    }

    [Fact]
    public async Task DuplicateShapeAndEvent_LockedTargetIsRefused_LockedSourceIsCopied()
    {
        const string addEvent = "add-frame-event Walk/2 name=Step";
        const string lockIdle = "set-chain-locked Idle value=true";
        const string lockWalk = "set-chain-locked Walk value=true";

        await Fails("hero.achx", "Locked", lockIdle, "duplicate-shape Walk/2/rect/Hitbox to=Idle/0");
        await Fails("hero.achx", "Locked", addEvent, lockIdle, "duplicate-frame-event Walk/2/event/0 to=Idle/0");
        await Fails("hero.achx", "Locked", lockWalk, "duplicate-shape Walk/2/rect/Hitbox");
        await Fails("hero.achx", "Locked", addEvent, lockWalk, "duplicate-frame-event Walk/2/event/0");
        await Ok("hero.achx", addEvent, lockWalk,
            "duplicate-shape Walk/2/rect/Hitbox to=Idle/0", "duplicate-frame-event Walk/2/event/0 to=Idle/0");

        var idle = Frames("hero.achx", "Idle")[0];
        Rects(idle).ShouldBe(["Hitbox"]);
        Events(idle).ShouldBe(["Step"]);
    }

    // ---- duplicate-frame-event ----

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task DuplicateFrameEvent_ToFrames_CopiesNameAndData(string file)
    {
        await Ok(file,
            "add-frame-event Walk/0 name=Step data=left",
            "add-frame-event Walk/0 name=Dust",
            "duplicate-frame-event Walk/0/event/* to=Walk/1-2",
            "duplicate-frame-event Walk/0/event/1 to=Idle/0");

        foreach (var frame in Frames(file, "Walk"))
            frame.Events.Select(e => (e.Name, e.Data)).ShouldBe([("Step", "left"), ("Dust", (string?)null)]);
        Events(Frames(file, "Idle")[0]).ShouldBe(["Dust"]);
    }

    [Fact]
    public async Task DuplicateFrameEvent_WithoutTo_AppendsToItsOwnFrame()
    {
        var changed = await Ok("hero.achx",
            "add-frame-event Walk/0 name=Step", "add-frame-event Walk/0 name=Dust", "duplicate-frame-event Walk/0/event/0");

        Events(Frames("hero.achx", "Walk")[0]).ShouldBe(["Step", "Dust", "Step"]);
        changed.ShouldContain("Walk/0/event/2");
    }

    [Fact]
    public async Task DuplicateFrameEvent_MissingEvent_IsIndexOutOfRange() =>
        await Fails("hero.achx", "IndexOutOfRange", "duplicate-frame-event Walk/0/event/0 to=Idle/0");

    [Fact]
    public async Task DuplicateFrameEvent_OnATsx_IsNotSupported() =>
        await Fails("tiles.tsx", "NotSupportedInTsx", "duplicate-frame-event Walk/0/event/0 to=Walk/1");
}

public class BulkDeleteTests : InlineBatchTests
{
    private string WalkEvents => string.Join(" | ", Frames("hero.achx", "Walk").Select(f => string.Join(",", Events(f))));

    /// <summary>Walk/0 gets A0 to A3, Walk/1 none, Walk/2 gets C0.</summary>
    private Task AddEvents() => Ok("hero.achx",
        "add-frame-event Walk/0 name=A0", "add-frame-event Walk/0 name=A1", "add-frame-event Walk/0 name=A2",
        "add-frame-event Walk/0 name=A3", "add-frame-event Walk/2 name=C0");

    [Fact]
    public async Task DeleteChain_Glob_DeletesEveryMatch()
    {
        await Ok("hero.achx", "add-chain WalkFast", "add-chain Run", "delete-chain Walk*");

        Chains("hero.achx").Select(c => c.Name).ShouldBe(["Idle", "Run"]);
    }

    [Fact]
    public async Task DeleteChain_GlobMatchingNothingOrALockedChain_WritesNothing()
    {
        await Fails("hero.achx", "NoMatch", "delete-chain Z*");
        await Fails("hero.achx", "Locked", "set-chain-locked Idle value=true", "delete-chain *");
    }

    [Fact]
    public async Task DeleteChain_Glob_WorksOnATsx()
    {
        await Ok("tiles.tsx", "delete-chain ID:*");

        Raw("tiles.tsx").ShouldNotContain("duration=\"150\"");
        Raw("tiles.tsx").ShouldContain("duration=\"200\"");
    }

    [Theory]
    [InlineData("*/*/event/*", " |  | ")]
    [InlineData("Walk/*/event/0", "A1,A2,A3 |  | ")]
    [InlineData("Walk/0/event/*", " |  | C0")]
    [InlineData("Walk/0/event/1-2", "A0,A3 |  | C0")]
    [InlineData("Walk/0/event/0,2", "A1,A3 |  | C0")]
    [InlineData("Walk/*/event/2,0", "A1,A3 |  | ")]
    [InlineData("Walk/*/event/1-9", "A0 |  | C0")]
    [InlineData("Walk/0/event/3", "A0,A1,A2 |  | C0")]
    public async Task DeleteFrameEvent_Selector_UsesIndexesFromBeforeTheOp(string address, string left)
    {
        await AddEvents();

        await Ok("hero.achx", $"delete-frame-event {address}");

        WalkEvents.ShouldBe(left);
    }

    [Theory]
    [InlineData("Walk/*/event/7", "NoMatch")]
    [InlineData("Walk/1/event/*", "NoMatch")]
    [InlineData("Walk/0/event/4", "IndexOutOfRange")]
    [InlineData("Walk/0-3/event/0", "IndexOutOfRange")]
    [InlineData("Walk/*/event/x", "InvalidAddress")]
    [InlineData("Walk/0/event/2-1", "InvalidAddress")]
    [InlineData("Walk/*/events/*", "InvalidAddress")]
    public async Task DeleteFrameEvent_BadSelector_WritesNothing(string address, string code)
    {
        await AddEvents();

        await Fails("hero.achx", code, $"delete-frame-event {address}");
    }

    /// <summary>A selector covering two chains of one name is refused even when the first holds nothing it could target.</summary>
    [Theory]
    [InlineData("set-frame-event */*/event/0 name=X")]
    [InlineData("delete-frame-event */*/event/*")]
    [InlineData("duplicate-frame-event */*/event/0 to=Walk/0")]
    [InlineData("set-shape-common */*/rect/* x=9")]
    [InlineData("delete-shape W/*/rect/Hitbox")]
    public async Task EventOrShapeSelector_ChainNameHeldTwice_IsAmbiguous(string op)
    {
        await AddEvents();
        Write("hero.achx", Raw("hero.achx").Replace("<Name>Idle</Name>", "<Name>W</Name>")
            .Replace("</AnimationChainArraySave>", Walk("hero.achx").Replace("<Name>Walk</Name>", "<Name>W</Name>") + "</AnimationChainArraySave>"));

        (await Fails("hero.achx", "Ambiguous", op)).ShouldContain("'W'");
    }

    [Theory]
    [InlineData("set-frame-length */* length=0.7")]
    [InlineData("duplicate-frame */* to=Walk")]
    [InlineData("duplicate-frame-event Walk/0/event/0 to=*/*")]
    public async Task FrameSelector_ChainNameHeldTwiceAndTheFirstIsEmpty_IsAmbiguous(string op)
    {
        await AddEvents();
        Write("hero.achx", Raw("hero.achx").Replace("<Name>Idle</Name>", "<Name>W</Name>")
            .Replace("<CoordinateType>Pixel</CoordinateType>", "<CoordinateType>Pixel</CoordinateType><AnimationChain><Name>W</Name></AnimationChain>"));

        (await Fails("hero.achx", "Ambiguous", op)).ShouldContain("'W'");
    }

    [Fact]
    public async Task ShapeSelector_ChainNameWithASlashAndNoSuchShape_IsStillSkipped()
    {
        Write("hero.achx", Raw("hero.achx").Replace("<Name>Idle</Name>", "<Name>A/B</Name>"));

        await Ok("hero.achx", "set-shape-common */*/rect/Hitbox x=9");

        Frames("hero.achx", "Walk")[2].ShapesSave!.AARectSaves.Single().X.ShouldBe(9f);
    }

    /// <summary>The Walk chain's XML.</summary>
    private string Walk(string file)
    {
        var xml = Raw(file);
        var start = xml.IndexOf("<AnimationChain>", StringComparison.Ordinal);
        return xml[start..(xml.IndexOf("</AnimationChain>", StringComparison.Ordinal) + "</AnimationChain>".Length)];
    }

    [Fact]
    public async Task SetFrameEvent_Selector_SkipsFramesWithoutTheEvent()
    {
        await AddEvents();

        await Ok("hero.achx", "set-frame-event Walk/*/event/0 name=X");

        WalkEvents.ShouldBe("X,A1,A2,A3 |  | X");
    }
}
