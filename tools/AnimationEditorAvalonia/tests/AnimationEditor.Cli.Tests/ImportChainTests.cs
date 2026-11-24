using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>import-chain copies chains from another animation file and only reads that file (#1948).</summary>
public class ImportChainTests : InlineBatchTests
{
    private const string Into = "dest/into.achx";
    private const string Source = "art/a.achx";

    public ImportChainTests()
    {
        Directory.CreateDirectory(PathOf("dest"));
        Directory.CreateDirectory(PathOf("art"));
        Write(Into, Doc("Second", "Pixel"));
    }

    private static string Doc(string unit, string coordinates, params string[] chains) =>
        "<AnimationChainArraySave><FileRelativeTextures>true</FileRelativeTextures>"
        + $"<TimeMeasurementUnit>{unit}</TimeMeasurementUnit><CoordinateType>{coordinates}</CoordinateType>"
        + string.Concat(chains) + "</AnimationChainArraySave>";

    private static string Chain(string name, params string[] frames) =>
        $"<AnimationChain><Name>{name}</Name>{string.Concat(frames)}</AnimationChain>";

    private static string Frame(string texture, string length = "0.1", string right = "16", string shapes = "") =>
        $"<Frame><TextureName>{texture}</TextureName><FrameLength>{length}</FrameLength><LeftCoordinate>0</LeftCoordinate>"
        + $"<RightCoordinate>{right}</RightCoordinate><TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>{shapes}</Frame>";

    private string[] Names(string file = Into) => Chains(file).Select(c => c.Name).ToArray();

    /// <summary>The chain alone as XML, which holds every field either format stores.</summary>
    private string Xml(string file, string chain)
    {
        var one = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        one.AnimationChains.Add(Chains(file).Single(c => c.Name == chain));
        return one.ToXmlString();
    }

    /// <summary>A failing batch on <see cref="Into"/> that leaves <see cref="Source"/> alone too.</summary>
    private async Task<string> Refused(string code, params string[] ops)
    {
        var before = Raw(Source);
        var message = await Fails(Into, code, ops);
        Raw(Source).ShouldBe(before);
        return message;
    }

    // ---- what comes along ----

    [Theory]
    [InlineData("hero.achx", "dest/into.achx")]
    [InlineData("hero.achj", "dest/into.achx")]
    [InlineData("hero.achx", "dest/into.achj")]
    [InlineData("hero.achj", "dest/into.achj")]
    public async Task Import_KeepsEveryStoredFieldAndOnlyReadsTheSource(string source, string into)
    {
        Write("dest/into.achj", """{"timeMeasurementUnit":"Second","coordinateType":"Pixel","animationChains":[]}""");
        await Ok(source,
            "set-chain-loop Walk value=false", "set-chain-pingpong Walk value=true", "set-chain-reverse Walk value=true",
            "set-frame-flip Walk/2 flipV=true", "set-frame-offset Walk/2 x=3 y=-4", "set-frame-color Walk/2 red=10 alpha=40 operation=add",
            "set-frame-keyframe-curve Walk/2 group=color curve=ease-in-out", "set-frame-keyframe Walk/2 group=offset value=false",
            "set-shape-keyframe-curve Walk/2/rect/Hitbox group=size curve=linear",
            "add-shape Walk/1 type=polygon name=Outline", "add-frame-event Walk/2 name=Step data=left",
            "set-chain-locked Walk value=true");
        // FRB1's own shape fields, which no op sets.
        if (source.EndsWith(".achx")) Write(source, Raw(source).Replace("<Z>0</Z>", "<Z>7</Z>").Replace("<Red>1</Red>", "<Red>0.25</Red>"));
        var before = Raw(source);

        (await Ok(into, $"import-chain Walk from=../{source}")).ShouldBe(["Walk"]);

        Xml(into, "Walk").ShouldBe(Xml(source, "Walk").Replace("sheet.png", "../sheet.png"));
        Chains(into).Single().IsLocked.ShouldBeTrue();
        Raw(source).ShouldBe(before);
    }

    [Theory]
    [InlineData("Millisecond", "250", "Second", 0.25f)]
    [InlineData("Second", "0.25", "Millisecond", 250f)]
    [InlineData("Undefined", "0.25", "Millisecond", 250f)]
    [InlineData("Millisecond", "250", "Undefined", 0.25f)]
    [InlineData("Second", "0.25", "Undefined", 0.25f)]
    public async Task Import_ConvertsFrameLengthsToTheReceivingFilesUnit(string from, string length, string to, float expected)
    {
        Write(Source, Doc(from, "Pixel", Chain("Run", Frame("../sheet.png", length))));
        Write(Into, Doc(to, "Pixel"));

        await Ok(Into, "import-chain Run from=../art/a.achx");

        Frames(Into, "Run").Single().FrameLength.ShouldBe(expected);
    }

    [Fact]
    public async Task Import_StoresTexturesRelativeToTheReceivingFileWithoutCopyingThem()
    {
        Write(Source, Doc("Second", "Pixel",
            Chain("Run", Frame("../sheet.png"), Frame(PathOf("other.png")), Frame("../dest/mine.png"), Frame(""))));
        File.Copy(PathOf("sheet.png"), PathOf("dest/mine.png"));

        await Ok(Into, "import-chain Run from=../art/a.achx");

        Frames(Into, "Run").Select(f => f.TextureName).ShouldBe(["../sheet.png", "../other.png", "mine.png", ""]);
        Directory.GetFiles(PathOf("dest"), "*.png").Length.ShouldBe(1);
    }

    // ---- which chains, in which order ----

    [Fact]
    public async Task Import_Glob_LandsAtTheEndInSourceOrder()
    {
        Write(Source, Doc("Second", "Pixel", Chain("RunB"), Chain("Jump"), Chain("RunA")));

        // Earlier ops leave the editor's selection on a chain that is not the last one, which a plain paste lands after.
        var changed = await Ok(Into, "add-chain First", "add-chain Last", "duplicate-chain First name=Second",
            "import-chain Jump from=../art/a.achx", "reorder-chain Jump index=0", "import-chain Run* from=../art/a.achx");

        Names().ShouldBe(["Jump", "First", "Second", "Last", "RunB", "RunA"]);
        changed.TakeLast(2).ShouldBe(["RunB", "RunA"]);
    }

    [Fact]
    public async Task Import_Name_GivesTheOneCopyAnotherName()
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run"), Chain("Jump")));
        Write(Into, Doc("Second", "Pixel", Chain("Run")));

        var changed = await Ok(Into, "import-chain Run from=../art/a.achx name=Dash", "import-chain J* from=../art/a.achx name=Hop");

        changed.ShouldBe(["Dash", "Hop"]);
        Names().ShouldBe(["Run", "Dash", "Hop"]);
        Names(Source).ShouldBe(["Run", "Jump"]);
    }

    [Theory]
    [InlineData("import-chain * from=../art/a.achx name=All", "InvalidValue", "2 chains")]
    [InlineData("import-chain Run from=../art/a.achx name=A/B", "InvalidAddress", "A/B")]
    [InlineData("import-chain Run from=../art/a.achx name=\" \"", "EmptyName", "empty")]
    [InlineData("import-chain Run from=../art/a.achx name=Here", "DuplicateName", "Here")]
    [InlineData("import-chain Z* from=../art/a.achx", "NoMatch", "a.achx")]
    [InlineData("import-chain Rum from=../art/a.achx", "NotFound", "Did you mean 'Run'?")]
    [InlineData("import-chain Run from=../art/none.achx", "NotFound", "none.achx")]
    [InlineData("import-chain Run from=../sheet.png", "UnsupportedFile", ".achx")]
    [InlineData("import-chain Run from=\"\"", "UnsupportedFile", ".achx")]
    [InlineData("import-chain Run from=../art/bad.achx", "IoFailed", "bad.achx")]
    [InlineData("import-chain Run from=../tiles.tsx", "WrongFileKind", ".tsx")]
    [InlineData("import-chain Run from=../tiles.TSX", "WrongFileKind", ".tsx")]
    [InlineData("import-chain Here from=into.achx", "Refused", "duplicate-chain")]
    [InlineData("import-chain Here from=../dest/./into.achx name=New", "Refused", "duplicate-chain")]
    public async Task Import_BadOp_WritesNothing(string op, string code, string says)
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run"), Chain("Jump")));
        Write("art/bad.achx", "<AnimationChainArraySave>");
        Write(Into, Doc("Second", "Pixel", Chain("Here")));

        (await Refused(code, op)).ShouldContain(says);
    }

    [Fact]
    public async Task Import_NamesTaken_ListsEveryClashAndWritesNothing()
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run"), Chain("Jump"), Chain("Free")));
        Write(Into, Doc("Second", "Pixel", Chain("Jump"), Chain("Run")));

        var message = await Refused("DuplicateName", "import-chain * from=../art/a.achx");

        message.ShouldContain("'Run', 'Jump'");
        message.ShouldNotContain("Free");
        message.ShouldContain("name=");
    }

    [Theory]
    [InlineData("import-chain Run from=../art/a.achx")]
    [InlineData("import-chain * from=../art/a.achx")]
    public async Task Import_SourceNameHeldTwice_IsAmbiguous(string op)
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run"), Chain("Run"), Chain("Jump")));

        (await Refused("Ambiguous", op)).ShouldContain("'Run'");
    }

    [Fact]
    public async Task Import_OnATsx_IsNotSupported() =>
        await Fails("tiles.tsx", "NotSupportedInTsx", "import-chain Walk from=hero.achx");

    [Fact]
    public async Task ImportedChain_IsEditableLaterInTheBatch_UnlessItCameLocked()
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run", Frame("../sheet.png"))));
        await Ok(Into, "import-chain Run from=../art/a.achx", "set-frame-offset Run/0 x=5");
        Frames(Into, "Run").Single().RelativeX.ShouldBe(5f);

        await Ok(Source, "set-chain-locked Run value=true");
        await Refused("Locked", "import-chain Run from=../art/a.achx name=Two", "set-frame-offset Two/0 x=5");
    }

    // ---- sources with problems of their own ----

    [Fact]
    public async Task Import_CheckErrorsOfTheSource_ComeAlongWithAWarning()
    {
        const string twins = "<ShapeCollectionSave><AxisAlignedRectangleSaves><AxisAlignedRectangleSave><Name>H</Name></AxisAlignedRectangleSave>"
            + "<AxisAlignedRectangleSave><Name>H</Name></AxisAlignedRectangleSave></AxisAlignedRectangleSaves></ShapeCollectionSave>";
        Write(Source, Doc("Second", "Pixel", Chain("Run", Frame("gone.png", shapes: twins))));

        var warnings = await OkWarnings(Into, "import-chain Run from=../art/a.achx");

        Frames(Into, "Run").Single().TextureName.ShouldBe("../art/gone.png");
        warnings.Length.ShouldBe(2);
        warnings.ShouldContain(w => w.Contains("[missing-texture]") && w.Contains("../art/gone.png"));
        warnings.ShouldContain(w => w.Contains("[duplicate-shape-name]") && w.Contains("Run/0"));

        // Only what the import brings is let through: another op raising the count still fails the batch.
        await Refused("WouldBreakFile", "import-chain Run from=../art/a.achx name=Two", "duplicate-frame Run/0");
    }

    [Fact]
    public async Task Import_CheckErrorFixedLaterInTheBatch_GivesNoWarning()
    {
        Write(Source, Doc("Second", "Pixel", Chain("Run", Frame("gone.png"))));

        var warnings = await OkWarnings(Into, "import-chain Run from=../art/a.achx", "set-frame-texture Run/0 texture=../sheet.png");

        warnings.ShouldBeEmpty();
    }

    /// <summary>Only the imported chains' textures have to be there.</summary>
    [Fact]
    public async Task Import_UvSource_ComesInAsPixels()
    {
        Write(Source, Doc("Second", "UV", Chain("Run", Frame("../sheet.png", right: "0.5")), Chain("Lost", Frame("gone.png", right: "0.5"))));

        await Ok(Into, "import-chain Run from=../art/a.achx");

        // sheet.png is 48x32.
        var frame = Frames(Into, "Run").Single();
        (frame.RightCoordinate, frame.BottomCoordinate).ShouldBe((24f, 32f));
    }

    [Fact]
    public async Task Import_UvSourceWhoseTextureIsMissing_IsRefused()
    {
        Write(Source, Doc("Second", "UV", Chain("Run", Frame("gone.png", right: "0.5"))));

        (await Refused("UnsupportedFile", "import-chain Run from=../art/a.achx")).ShouldContain("gone.png");
    }

    [Fact]
    public void Schema_SaysWhereChainsLandAndThatAClashWritesNothing()
    {
        var text = EditOps.Specs.Single(s => s.Name == "import-chain").Description;

        text.ShouldContain("at the end, in the source file's order");
        text.ShouldContain("nothing is written");
    }
}
