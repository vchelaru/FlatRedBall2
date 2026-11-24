using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>set-chain-length, invert-frame-order, justify-offsets-bottom and match-frame-size.</summary>
public class ChainOpsTests : InlineBatchTests
{
    private const string LockWalk = "set-chain-locked Walk value=true";

    private void ShouldHaveLengths(string file, string chain, params float[] expected)
    {
        var actual = Frames(file, chain).Select(f => f.FrameLength).ToArray();
        actual.Length.ShouldBe(expected.Length);
        for (int i = 0; i < expected.Length; i++) actual[i].ShouldBe(expected[i], expected[i] * 1e-5f);
    }

    /// <summary>loose.achx: one chain, Walk, with one frame per fragment; its texture is not on disk.</summary>
    private void WriteUntextured(params string[] frames) => Write("loose.achx", $"""
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>Pixel</CoordinateType>
          <AnimationChain><Name>Walk</Name>{string.Concat(frames.Select(f => $"<Frame><TextureName>gone.png</TextureName>{f}</Frame>"))}</AnimationChain>
        </AnimationChainArraySave>
        """);

    private static string Region(int left, int top, int right, int bottom, float length = 0.1f) => FormattableString.Invariant(
        $"<FrameLength>{length}</FrameLength><LeftCoordinate>{left}</LeftCoordinate><RightCoordinate>{right}</RightCoordinate><TopCoordinate>{top}</TopCoordinate><BottomCoordinate>{bottom}</BottomCoordinate>");

    private const string UnknownRegion = "<FrameLength>0.1</FrameLength>";

    // ---- set-chain-length ----

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task SetChainLength_Default_KeepsTheRatios(string file)
    {
        var changed = await Ok(file, "set-chain-length Walk length=0.8");

        changed.ShouldBe(["Walk"]);
        ShouldHaveLengths(file, "Walk", 0.2f, 0.2f, 0.4f);
    }

    [Fact]
    public async Task SetChainLength_Even_GivesEveryFrameTheSameLength()
    {
        await Ok("hero.achx", "set-chain-length Walk length=0.6 mode=even");

        ShouldHaveLengths("hero.achx", "Walk", 0.2f, 0.2f, 0.2f);
    }

    [Fact]
    public async Task SetChainLength_Glob_SetsEachChainInTheFilesUnit()
    {
        await Ok("hero.achx", "set-unit unit=millisecond", "set-chain-length * length=800");

        ShouldHaveLengths("hero.achx", "Walk", 200f, 200f, 400f);
        ShouldHaveLengths("hero.achx", "Idle", 800f);
    }

    [Theory]
    [InlineData("set-chain-length Walk length=0.4")]
    [InlineData("set-chain-length Walk length=0.7")]
    [InlineData("set-chain-length Walk length=0.7 mode=even")]
    public async Task SetChainLength_AlreadyThatLength_LeavesTheFileByteIdentical(string op)
    {
        await Ok("hero.achx", op);
        var before = Raw("hero.achx");

        (await Ok("hero.achx", op)).ShouldBeEmpty();

        Raw("hero.achx").ShouldBe(before);
    }

    // The op sets one forward pass; describe counts a ping-pong chain's played cycle, twice as long.
    [Fact]
    public async Task SetChainLength_OnPingPong_SetsTheSumOfTheFrameLengths()
    {
        await Ok("hero.achx", "set-chain-pingpong Walk value=true", "set-chain-length Walk length=0.8");

        ShouldHaveLengths("hero.achx", "Walk", 0.2f, 0.2f, 0.4f);
        (await Describe("hero.achx")).ShouldContain("Walk  (3 frames, length 1.6s");
    }

    // Walk has three frames: 0.002 s leaves each under 0.001 s, 1000 s puts the longest above 60 s.
    [Theory]
    [InlineData("length=0")]
    [InlineData("length=-1")]
    [InlineData("length=0.002")]
    [InlineData("length=0.002 mode=even")]
    [InlineData("length=1000")]
    [InlineData("length=1e30")]
    [InlineData("length=1e300")]
    public async Task SetChainLength_FrameOutOfRange_FailsNamingTheFrameAndTheRange(string fields)
    {
        var message = await Fails("hero.achx", "InvalidValue", $"set-chain-length Walk {fields}");

        message.ShouldContain("Walk/");
        message.ShouldContain("0.001 to 60 seconds");
    }

    [Theory]
    [InlineData("proportional")]
    [InlineData("even")]
    public async Task SetChainLength_NoFrames_Fails(string mode) =>
        (await Fails("hero.achx", "InvalidValue", "add-chain Empty", $"set-chain-length Empty length=1 mode={mode}"))
            .ShouldContain("no frames");

    [Fact]
    public async Task SetChainLength_ZeroTotal_FailsProportionalButEvenWorks()
    {
        WriteUntextured(Region(0, 0, 16, 32, length: 0), Region(0, 0, 16, 32, length: 0));

        (await Fails("loose.achx", "InvalidValue", "set-chain-length Walk length=1")).ShouldContain("mode=even");

        await Ok("loose.achx", "set-chain-length Walk length=1 mode=even");
        ShouldHaveLengths("loose.achx", "Walk", 0.5f, 0.5f);
    }

    [Fact]
    public async Task SetChainLength_OneZeroLengthFrame_FailsProportional()
    {
        WriteUntextured(Region(0, 0, 16, 32), Region(0, 0, 16, 32, length: 0));

        (await Fails("loose.achx", "InvalidValue", "set-chain-length Walk length=1")).ShouldContain("Walk/1");
    }

    // 9 x 0.1 scaled to 0.009 comes out as 0.00099999993 per frame, just under the 0.001 minimum.
    [Fact]
    public async Task SetChainLength_FloatNoiseAtTheMinimum_IsAccepted()
    {
        WriteUntextured(Enumerable.Repeat(Region(0, 0, 16, 32), 9).ToArray());

        await Ok("loose.achx", "set-chain-length Walk length=0.009");
    }

    // ---- invert-frame-order ----

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task InvertFrameOrder_ReversesFramesWithTheirShapes(string file)
    {
        var changed = await Ok(file, "invert-frame-order Walk");

        changed.ShouldBe(["Walk"]);
        var frames = Frames(file, "Walk");
        frames.Select(f => f.LeftCoordinate).ShouldBe([32f, 16f, 0f]);
        frames[0].ShapesSave!.AARectSaves.Single().Name.ShouldBe("Hitbox");
    }

    [Fact]
    public async Task InvertFrameOrder_Glob_LeavesAOneFrameChainUnchanged() =>
        (await Ok("hero.achx", "invert-frame-order *")).ShouldBe(["Walk"]);

    // ---- justify-offsets-bottom ----

    [Fact]
    public async Task JustifyOffsetsBottom_SetsYToHalfTheRegionHeightAndKeepsX()
    {
        var changed = await Ok("hero.achx",
            "set-frame-offset Walk/1 x=3 y=-2", "set-frame-region Walk/2 region=0,0,16,20", "justify-offsets-bottom Walk");

        changed.ShouldContain("Walk");
        Frames("hero.achx", "Walk").Select(f => (f.RelativeX, f.RelativeY)).ShouldBe([(0f, 16f), (3f, 16f), (0f, 10f)]);
    }

    [Fact]
    public async Task JustifyOffsetsBottom_DiagonalFlip_UsesTheDisplayedHeight()
    {
        await Ok("hero.achx", "set-frame-flip Walk/1 flipDiagonal=true", "justify-offsets-bottom Walk");

        // A 16 x 32 region: frame 1 is drawn 32 x 16.
        Frames("hero.achx", "Walk").Select(f => f.RelativeY).ShouldBe([16f, 8f, 16f]);
    }

    [Fact]
    public async Task JustifyOffsetsBottom_Twice_ChangesNothingTheSecondTime()
    {
        (await Ok("hero.achx", "justify-offsets-bottom *")).ShouldBe(["Walk", "Idle"]);

        (await Ok("hero.achx", "justify-offsets-bottom *")).ShouldBeEmpty();
    }

    [Fact]
    public async Task JustifyOffsetsBottom_KnownRegionWithAMissingTexture_Works()
    {
        WriteUntextured(Region(0, 0, 16, 32));

        await Ok("loose.achx", "justify-offsets-bottom Walk");

        Frames("loose.achx", "Walk")[0].RelativeY.ShouldBe(16f);
    }

    [Fact]
    public async Task JustifyOffsetsBottom_OneFrameWithAnUnknownSize_FailsNamingIt()
    {
        WriteUntextured(Region(0, 0, 16, 32), UnknownRegion);

        (await Fails("loose.achx", "UnsupportedFile", "justify-offsets-bottom Walk")).ShouldContain("Walk/1");
    }

    [Theory]
    [InlineData("justify-offsets-bottom Walk")]
    [InlineData("match-frame-size Walk/0/rect/Box")]
    public async Task RegionOfNoHeight_Fails(string op)
    {
        WriteUntextured(Region(0, 8, 16, 8));

        (await Fails("loose.achx", "InvalidValue", "add-shape Walk/0 type=rect name=Box", op)).ShouldContain("Walk/0");
    }

    // ---- match-frame-size ----

    [Fact]
    public async Task MatchFrameSize_CentersOnTheOffsetAndTakesHalfTheRegion()
    {
        var changed = await Ok("hero.achx", "set-frame-offset Walk/2 x=3 y=-2", "match-frame-size Walk/2/rect/Hitbox");

        changed.ShouldContain("Walk/2/rect/Hitbox");
        var rect = Frames("hero.achx", "Walk")[2].ShapesSave!.AARectSaves.Single();
        (rect.X, rect.Y, rect.ScaleX, rect.ScaleY).ShouldBe((3f, -2f, 8f, 16f));
        (await Ok("hero.achx", "match-frame-size Walk/2/rect/Hitbox")).ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchFrameSize_DiagonalFlip_TakesTheDisplayedSize()
    {
        await Ok("hero.achx", "set-frame-flip Walk/2 flipDiagonal=true", "match-frame-size Walk/2/rect/Hitbox");

        // A 16 x 32 region, drawn 32 x 16.
        var rect = Frames("hero.achx", "Walk")[2].ShapesSave!.AARectSaves.Single();
        (rect.ScaleX, rect.ScaleY).ShouldBe((16f, 8f));
    }

    [Fact]
    public async Task MatchFrameSize_Selector_MatchesEachRectToItsOwnFrame()
    {
        await Ok("hero.achx",
            "duplicate-shape Walk/2/rect/Hitbox to=Walk/0", "set-frame-region Walk/0 region=0,0,10,20", "match-frame-size Walk/*/rect/Hit*");

        Frames("hero.achx", "Walk").SelectMany(f => f.ShapesSave?.AARectSaves ?? []).Select(r => (r.ScaleX, r.ScaleY))
            .ShouldBe([(5f, 10f), (8f, 16f)]);
    }

    [Fact]
    public async Task MatchFrameSize_Circle_IsTheWrongShapeType() =>
        await Fails("hero.achx", "WrongShapeType", "match-frame-size Walk/2/circle/Foot");

    [Fact]
    public async Task MatchFrameSize_UnknownRegion_Fails()
    {
        WriteUntextured(UnknownRegion);

        (await Fails("loose.achx", "UnsupportedFile", "add-shape Walk/0 type=rect name=Box", "match-frame-size Walk/0/rect/Box"))
            .ShouldContain("Walk/0");
    }

    // ---- locked chains ----

    [Theory]
    [InlineData("set-chain-length Walk length=0.8")]
    [InlineData("set-chain-length * length=0.8")]
    [InlineData("invert-frame-order Walk")]
    [InlineData("invert-frame-order *")]
    [InlineData("justify-offsets-bottom Walk")]
    [InlineData("justify-offsets-bottom *")]
    [InlineData("match-frame-size Walk/2/rect/Hitbox")]
    [InlineData("match-frame-size */*/rect/*")]
    [InlineData("set-frame-texture Walk/0 texture=other.png")]
    [InlineData("set-frame-texture Walk/0-1 texture=other.png")]
    [InlineData("set-frame-texture */* texture=other.png")]
    public async Task LockedChain_IsRefused(string op) =>
        (await Fails("hero.achx", "Locked", LockWalk, op)).ShouldContain("Chain 'Walk' is locked; unlock it first (set-chain-locked).");

    // ---- .tsx ----

    [Fact]
    public async Task Tsx_LengthAndOrderAreSaved()
    {
        await Ok("tiles.tsx", "set-chain-length Walk length=0.8", "invert-frame-order Walk");

        var frames = Regex.Matches(Raw("tiles.tsx"), "<frame tileid=\"(\\d+)\" duration=\"(\\d+)\"")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToArray();
        frames[..3].ShouldBe([("2", "400"), ("1", "200"), ("0", "200")]);
    }

    [Theory]
    [InlineData("justify-offsets-bottom Walk")]
    [InlineData("match-frame-size Walk/0/rect/Hitbox")]
    public async Task Tsx_OffsetsAndShapesAreNotSupported(string op) =>
        await Fails("tiles.tsx", "NotSupportedInTsx", op);
}
