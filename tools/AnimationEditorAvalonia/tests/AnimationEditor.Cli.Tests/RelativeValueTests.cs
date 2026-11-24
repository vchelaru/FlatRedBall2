using System.Globalization;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>Relative number values (+2, "- 2", *2, /2) and the range of every number field of the edit ops (#1942).</summary>
public class RelativeValueTests : InlineBatchTests
{
    private const string AddPolygon = "add-shape Walk/0 type=polygon name=Zone";

    /// <summary>For every op with a number or integer field, the rest of a valid op.</summary>
    private static readonly Dictionary<string, string> AchxOps = new()
    {
        ["reorder-chain"] = "Walk", ["reorder-frame"] = "Walk/0", ["duplicate-frame"] = "Walk/0", ["reorder-shape"] = "Walk/2/rect/Hitbox",
        ["add-frame"] = "Walk texture=sheet.png", ["add-frames-grid"] = "Walk texture=sheet.png cellWidth=16 cellHeight=16 columns=1 rows=1",
        ["set-chain-length"] = "Walk", ["set-frame-length"] = "Walk/0", ["set-frame-offset"] = "Walk/0", ["set-frame-color"] = "Walk/0",
        ["set-rect"] = "Walk/2/rect/Hitbox", ["set-circle"] = "Walk/2/circle/Foot", ["set-polygon"] = "Walk/0/polygon/Zone",
        ["set-shape-common"] = "Walk/2/rect/Hitbox", ["set-chain-owner-tile"] = "Walk",
    };

    private static readonly Dictionary<string, string> SeqOps = new()
    {
        ["reorder-sequence"] = "Run", ["duplicate-track"] = "Run/Body to=Rest", ["add-track"] = "Run", ["reorder-track"] = "Run/Body",
        ["add-reference-track"] = "Run target=Rest", ["add-block"] = "Run/Body chain=Walk", ["set-block-start"] = "Run/Body/0",
        ["set-block-length"] = "Run/Body/0", ["set-block-end"] = "Run/Body/0", ["duplicate-block"] = "Run/Body/0", ["move-block"] = "Run/Body/0",
    };

    private static TheoryData<string, string, string> FieldsWhere(Func<OpField, bool> match)
    {
        var data = new TheoryData<string, string, string>();
        foreach (var (file, specs, ops) in new[] { ("hero.achx", EditOps.Specs, AchxOps), ("run.seqj", SequenceOps.Specs, SeqOps) })
            foreach (var spec in specs)
                foreach (var field in spec.Fields.Where(match))
                {
                    var others = ops[spec.Name].Split(' ').Where(part => !part.StartsWith(field.Name + "="));
                    data.Add(file, $"{spec.Name} {string.Join(" ", others)}", field.Name);
                }
        return data;
    }

    public static TheoryData<string, string, string> NumberFields() => FieldsWhere(f => f.Type == "number");
    public static TheoryData<string, string, string> RelativeFields() => FieldsWhere(f => f.Relative);
    public static TheoryData<string, string, string> AbsoluteFields() => FieldsWhere(f => f.Type is "number" or "integer" && !f.Relative);

    /// <summary>run.seqj: Run/Body [Walk@0 for 0.8 s, Walk@1 auto (0.4 s)], Run/Sub [clip of Rest@0 auto (0.5 s)], Rest/Body [Idle@0 for 0.5 s].</summary>
    public RelativeValueTests()
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var run = new SequenceSave { Name = "Run" };
        run.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains =
            {
                new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Length = 0.8 },
                new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 1 },
            },
        });
        run.Tracks.Add(new SequenceTrackSave { Name = "Sub", References = [new SequenceReferenceSave { Sequence = "Rest", Start = 0 }] });
        var rest = new SequenceSave { Name = "Rest" };
        rest.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Idle", Start = 0, Length = 0.5 } },
        });
        save.Sequences.Add(run);
        save.Sequences.Add(rest);
        Write("run.seqj", save.ToJsonString());
    }

    private static string[] Batch(string file, string op) => file == "run.seqj" ? [op] : [AddPolygon, op];

    private SequenceListSave Sequences => SequenceListSave.FromJsonString(Raw("run.seqj"));

    private AARectSave Hitbox => Frames("hero.achx", "Walk")[2].ShapesSave!.AARectSaves.Single();

    // ---- every field of every op ----

    [Theory]
    [MemberData(nameof(NumberFields))]
    public async Task NumberField_OutOfRange_FailsWithAMessage(string file, string op, string field)
    {
        foreach (var value in new[] { "1e30", "-1e30", "1e300" })
            (await Fails(file, "InvalidValue", Batch(file, $"{op} {field}={value}"))).ShouldNotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(RelativeFields))]
    public async Task RelativeField_AppliesToTheCurrentValue(string file, string op, string field)
    {
        var value = field is "red" or "green" or "blue" or "alpha" ? "- 1" : "+0.125";

        (await Ok(file, Batch(file, $"{op} {field}=\"{value}\""))).ShouldContain(op.Split(' ')[1]);
    }

    [Theory]
    [MemberData(nameof(RelativeFields))]
    public async Task RelativeField_OverflowingResult_FailsWithAMessage(string file, string op, string field)
    {
        var max = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);

        (await Fails(file, "InvalidValue", Batch(file, $"{op} {field}=+{max}"))).ShouldNotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(AbsoluteFields))]
    public async Task AbsoluteField_RelativeValue_IsAUsageErrorNamingTheOp(string file, string op, string field)
    {
        foreach (var value in new[] { "+1", "- 1", "*2" })
        {
            var error = await UsageError(file, $"{op} {field}=\"{value}\"");

            error.ShouldContain($"field '{field}' of '{op.Split(' ')[0]}' must be an absolute ");
            error.ShouldContain(value);
        }
    }

    [Theory]
    [InlineData("set-polygon Walk/0/polygon/Zone points=[[\"+1\",0],[6,0],[6,6]]")]
    [InlineData("set-polygon Walk/0/polygon/Zone points=[[+1,0],[6,0],[6,6]]")]
    public async Task PolygonPoints_RelativeValue_IsAUsageError(string op)
    {
        var error = await UsageError("hero.achx", op);

        error.ShouldContain("set-polygon");
        error.ShouldContain("absolute");
    }

    [Fact]
    public async Task PolygonPoints_OutOfRange_Fails() =>
        await Fails("hero.achx", "InvalidValue", AddPolygon, "set-polygon Walk/0/polygon/Zone points=[[1e30,0],[6,0],[6,6]]");

    [Theory]
    [InlineData("set-frame-region Walk/0 region=+8,0,16,16")]
    [InlineData("set-frame-region Walk/0 region=2147483647,0,1,16")]
    [InlineData("add-frame Walk texture=sheet.png region=2147483647,0,1,16")]
    public async Task Region_RelativeOrOutOfRange_Fails(string op) => await Fails("hero.achx", "InvalidValue", op);

    // ---- values ----

    [Theory]
    [InlineData("x=+2", 6f)]
    [InlineData("x=\"- 2\"", 2f)]
    [InlineData("x=*2", 8f)]
    [InlineData("x=/2", 2f)]
    [InlineData("x=-2", -2f)]
    [InlineData("x=3+4", 7f)]
    [InlineData("x=1e1", 10f)]
    public async Task InlineValue_IsRelativeOnlyWithALeadingOperator(string value, float expected)
    {
        await Ok("hero.achx", "set-frame-offset Walk/0 x=4 y=1", $"set-frame-offset Walk/0 {value}");

        var frame = Frames("hero.achx", "Walk")[0];
        (frame.RelativeX, frame.RelativeY).ShouldBe((expected, 1f));
    }

    [Fact]
    public void Parse_JsonNumberIsAbsolute_StringIsParsedLikeTheInspector()
    {
        static OpsParse Offset(string x) => EditOps.Parse($$"""{"ops":[{"op":"set-frame-offset","frame":"Walk/0","x":{{x}}}]}""");

        Offset("4").Ops![0].Values["x"].ShouldBe("4");
        Offset("\"-4\"").Ops![0].Values["x"].ShouldBe("-4");
        Offset("\"3 + 4\"").Ops![0].Values["x"].ShouldBe("7");
        Offset("\"+2\"").Ops![0].Values["x"].ShouldBe("+2");
        Offset("\"+\"").Error!.ShouldContain("must be a number");
        Offset("\"/0\"").Error!.ShouldContain("must be a number");
        EditOps.Parse("""{"ops":[{"op":"set-frame-color","frame":"Walk/0","red":"1.5"}]}""").Error!.ShouldContain("must be an integer");
        EditOps.Parse("""{"ops":[{"op":"reorder-frame","frame":"Walk/0","index":"1"}]}""").Error!.ShouldContain("must be an integer");
    }

    [Fact]
    public async Task Selector_ChangesEachTargetFromItsOwnValue()
    {
        await Ok("hero.achx", "set-frame-offset Walk/1 x=10", "set-frame-offset Walk/* x=+1.5", "set-frame-length Walk/* length=*2");

        var frames = Frames("hero.achx", "Walk");
        frames.Select(f => f.RelativeX).ShouldBe([1.5f, 11.5f, 1.5f]);
        frames.Select(f => f.FrameLength).ShouldBe([0.2f, 0.2f, 0.4f]);
    }

    [Fact]
    public async Task ShapeFields_ChangeFromTheShapesOwnValues()
    {
        await Ok("hero.achx", "set-rect Walk/2/rect/Hitbox x=+1 y=\"- 1\" scaleX=*2 scaleY=/2", "set-shape-common Walk/2/rect/* x=+1");

        (Hitbox.X, Hitbox.Y, Hitbox.ScaleX, Hitbox.ScaleY).ShouldBe((3f, 1f, 8f, 3f));
    }

    [Fact]
    public async Task SetChainLength_Relative_StartsFromTheSumOfItsFrames()
    {
        await Ok("hero.achx", "set-chain-length * length=*2");

        Frames("hero.achx", "Walk").Select(f => f.FrameLength).ShouldBe([0.2f, 0.2f, 0.4f]);
        Frames("hero.achx", "Idle").Single().FrameLength.ShouldBe(1f);
    }

    /// <summary>An unset channel starts from what it inherits, else 255 (0 for red, green and blue in add), as in the Inspector, read before any target changes.</summary>
    [Fact]
    public async Task ColorChannel_Unset_StartsFromTheInheritedValue()
    {
        await Ok("hero.achx",
            "set-frame-color Walk/0 red=100 operation=add",
            "set-frame-color Walk/* red=+10 green=+20 alpha=\"- 5\"");

        Frames("hero.achx", "Walk").Select(f => (f.Red, f.Green, f.Alpha))
            .ShouldBe([(110, 20, 250), (110, 20, 250), (110, 20, 250)]);
    }

    // ---- refusals ----

    [Theory]
    [InlineData("set-frame-length Walk/* length=\"- 0.15\"", "target Walk/0: ", "0.001 to 60 seconds")]
    [InlineData("set-frame-color Walk/* red=+10", "target Walk/0: ", "-255 to 255")]
    [InlineData("set-rect Walk/2/rect/* scaleX=\"- 4\"", "target Walk/2/rect/Hitbox: ", "greater than zero")]
    public async Task OutOfRangeResult_FailsTheBatchLikeAnAbsoluteValue(string op, string target, string range)
    {
        var message = await Fails("hero.achx", "InvalidValue", "set-frame-offset Walk/0 x=1", op);

        message.ShouldContain(target);
        message.ShouldContain(range);
    }

    [Theory]
    [InlineData("set-frame-offset Walk/0 x=+1", "Locked")]
    [InlineData("set-frame-color Walk/0 red=+1", "Locked")]
    [InlineData("set-rect Walk/2/rect/Hitbox x=+1", "Locked")]
    [InlineData("set-chain-length Walk length=+1", "Locked")]
    [InlineData("set-frame-offset Walk/9 x=+1", "IndexOutOfRange")]
    [InlineData("set-rect Walk/2/circle/Foot scaleX=+1", "WrongShapeType")]
    public async Task RelativeValue_FailsWithTheAbsoluteValuesCode(string op, string code)
    {
        await Fails("hero.achx", code, "set-chain-locked Walk value=true", op);
        await Fails("hero.achx", code, "set-chain-locked Walk value=true", op.Replace("=+1", "=1"));
    }

    [Fact]
    public async Task StoredValueTooLargeToAddTo_FailsRelativeAndCanBeSetAbsolute()
    {
        Write("hero.achx", Raw("hero.achx").Replace("<FrameLength>0.1</FrameLength>", "<FrameLength>0.1</FrameLength><RelativeX>1E+30</RelativeX>"));

        (await Fails("hero.achx", "InvalidValue", "set-frame-offset Walk/0 x=+1")).ShouldContain("absolute");
        await Ok("hero.achx", "set-frame-offset Walk/0 x=3");

        Frames("hero.achx", "Walk")[0].RelativeX.ShouldBe(3f);
    }

    // ---- sequences ----

    [Fact]
    public async Task BlockStartAndEnd_ChangeEachBlockFromItsOwn()
    {
        await Ok("run.seqj", "set-block-start Run/Body/* start=+0.25", "set-block-end Run/Body/* end=+0.1", "set-block-end Run/Sub/0 end=+0.5");

        Sequences.Sequences[0].Tracks[0].Chains.Select(c => (c.Start, c.Length)).ShouldBe([(0.25, 0.9), (1.25, 0.5)]);
        Sequences.Sequences[0].Tracks[1].References!.Single().Length.ShouldBe(1);
    }

    [Fact]
    public async Task BlockLength_Auto_StartsFromOneCycle()
    {
        await Ok("run.seqj", "set-block-length Run/*/* length=*2");

        Sequences.Sequences[0].Tracks[0].Chains.Select(c => c.Length).ShouldBe([1.6, 0.8]);
        Sequences.Sequences[0].Tracks[1].References!.Single().Length.ShouldBe(1);
    }

    [Fact]
    public async Task MoveBlock_RelativeStart_IsFromItsCurrentStart()
    {
        await Fails("run.seqj", "IndexOutOfRange", "move-block Run/Body/9 start=*2");
        await Ok("run.seqj", "move-block Run/Body/1 to=Rest/Body start=+0.5");

        Sequences.Sequences[1].Tracks[0].Chains[^1].Start.ShouldBe(1.5);
    }

    [Fact]
    public async Task BlockStart_OneTargetBelowZero_FailsTheBatch() =>
        (await Fails("run.seqj", "InvalidValue", "set-block-start Run/Body/* start=\"- 0.5\"")).ShouldContain("target Run/Body/0: ");

    // ---- schema ----

    [Fact]
    public void Schema_MarksRelativeFields()
    {
        EditJson.SchemaText().ShouldContain("x  number or relative  optional");
        EditJson.Render(EditJson.WriteSchema).ShouldContain("\"name\":\"x\",\"type\":\"number\",\"relative\":true");
    }
}
