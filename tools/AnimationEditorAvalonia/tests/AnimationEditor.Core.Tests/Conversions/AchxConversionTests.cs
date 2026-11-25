using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Conversions;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Conversions;

/// <summary>Convert to .achj: the plan is built without writing, converting runs it, and what happened is compared with it (#2135).</summary>
public sealed class AchxConversionTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly List<string> _recycled = new();

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_dir.Path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        _dir.Dispose();
    }

    private string At(string relative) => Path.Combine(_dir.Path, relative);

    private string Achx(string relative, string chain = "Walk")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(relative))!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = chain, Frames = { new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f } } });
        list.Save(At(relative));
        return At(relative);
    }

    private string Seqj(string relative, params string[] blockSources)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(relative))!);
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.AddRange(blockSources.Select((source, i) => new SequenceChainReferenceSave { Source = source, Chain = "Walk", Start = i }));
        var save = new SequenceListSave { Sequences = { new SequenceSave { Name = "Attack", Tracks = { track } } } };
        save.Sources.AddRange(blockSources.Distinct());
        save.Save(At(relative));
        return At(relative);
    }

    private AchxConversionPlan Plan(params string[] sources) => AchxConversionPlan.Build(sources, _dir.Path, OpenDocuments.None);

    private string? Recycle(string path)
    {
        _recycled.Add(Path.GetFileName(path));
        File.Delete(path);
        return null;
    }

    private ConversionSteps Steps(Func<string, string?>? recycle = null, Func<string, string?>? saveEdits = null) =>
        new(saveEdits ?? (_ => null), recycle ?? Recycle, (_, _) => throw new InvalidOperationException("no sequence is open"));

    private Dictionary<string, string> Contents() =>
        Directory.GetFiles(_dir.Path, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText);

    [Fact]
    public void Build_ListsEveryFileAndSequence_AndWritesNothing()
    {
        string hero = Achx("Sprites/hero.achx");
        string taken = Achx("Sprites/taken.achx");
        File.WriteAllText(At("Sprites/taken.achj"), "another file");
        File.WriteAllText(At("Sprites/broken.achx"), "<AnimationChain");
        string stale = Achx("Sprites/stale.achx");
        Seqj("Sequences/attack.seqj", "../Sprites/hero.achx", "../Sprites/hero.achx", "../Sprites/taken.achx");
        Seqj("Sequences/other.seqj", "hero.achx"); // same name, another folder
        Seqj("bin/copy.seqj", "../Sprites/hero.achx");
        File.SetAttributes(Seqj("Sequences/locked.seqj", "../Sprites/hero.achx"), FileAttributes.ReadOnly);
        File.WriteAllText(At("Sequences/damaged.seqj"), "{");
        var unsavedSequence = SequenceListSave.FromFile(Seqj("Sequences/unsaved.seqj", "../Sprites/hero.achx"));
        var open = new OpenDocuments(
            new Dictionary<FilePath, (bool, string?)> { [new FilePath(hero)] = (true, null), [new FilePath(stale)] = (true, "it changed") },
            new Dictionary<FilePath, SequenceListSave?> { [new FilePath(At("Sequences/unsaved.seqj"))] = unsavedSequence });
        var before = Contents();

        var plan = AchxConversionPlan.Build([hero, taken, At("Sprites/broken.achx"), stale], _dir.Path, open);

        Contents().ShouldBe(before);
        plan.Converted.Single().ShouldBe(new PlannedFile(hero, At("Sprites/hero.achj"), null, Animations: 1, Frames: 1, IsOpen: true, SavesEdits: true,
            Content: AchxConversionPlan.ContentOf(File.ReadAllText(hero))));
        plan.Skipped.Select(file => (file.Source, file.Problem!.Why)).ShouldBe(
            [(taken, "taken.achj exists"), (At("Sprites/broken.achx"), "Can't be read"), (stale, "Unsaved edits can't be saved")]);
        plan.Files[2].Problem!.Detail.ShouldNotBeEmpty();
        plan.Files[3].Problem!.Detail.ShouldBe("it changed");
        plan.Sequences.Select(sequence => (new FilePath(sequence.Path).NoPath, sequence.Blocks, sequence.IsOpen)).ShouldBe(
            [("attack.seqj", 2, false), ("damaged.seqj", 0, false), ("locked.seqj", 1, false), ("unsaved.seqj", 1, true)]);
        plan.Sequences[0].Problem.ShouldBeNull();
        plan.Sequences[1].Problem!.ShouldSatisfyAllConditions(
            problem => problem.Why.ShouldBe("Can't be read"), problem => problem.Detail.ShouldStartWith("What it plays is not known. "));
        plan.Sequences[2].Problem.ShouldBe(new ConversionProblem("Read-only", "The file is read-only."));
        plan.Sequences[3].Problem!.Why.ShouldBe("Edits can't be saved");
    }

    [Fact]
    public void Run_UpdatesAClosedSequence_KeepingEverythingElseInIt_AndRecyclesWhatItReplaced()
    {
        string hero = Achx("Sprites/hero.achx");
        string boss = Achx("Sprites/boss.achx");
        string attack = At("attack.seqj");
        // Windows line endings, a final newline, unknown properties, a backslash and a block spelled another way.
        string text = "{\r\n  \"version\": 1,\r\n  \"sources\": [\r\n    \"Sprites\\\\hero.achx\",\r\n    \"Sprites/boss.achx\"\r\n  ],\r\n  \"sequences\": [\r\n    {\r\n"
            + "      \"name\": \"Attack\",\r\n      \"loop\": true,\r\n      \"tracks\": [\r\n        {\r\n          \"name\": \"Body\",\r\n          \"chains\": [\r\n            {\r\n"
            + "              \"source\": \"Sprites\\\\HERO.achx\",\r\n              \"chain\": \"Walk\",\r\n              \"start\": 0,\r\n              \"tint\": \"red\"\r\n            }\r\n"
            + "          ]\r\n        }\r\n      ]\r\n    }\r\n  ],\r\n  \"tool\": 7\r\n}\r\n";
        File.WriteAllText(attack, text);
        var plan = Plan(hero);
        var doing = new List<string>();

        var outcome = AchxConversion.Run(plan, Steps() with { Progress = progress => doing.Add(progress.Text) });

        doing.ShouldBe(["Converting 1 of 1", "Updating sequence 1 of 1", "Moving to the Recycle Bin 1 of 1", "Comparing"]);
        outcome.Differences.ShouldBeEmpty();
        outcome.Converted.ShouldBe([hero]);
        outcome.Updated.ShouldBe([attack]);
        File.ReadAllText(attack).ShouldBe(text.Replace("hero.achx", "hero.achj").Replace("HERO.achx", "HERO.achj"));
        _recycled.ShouldBe(["attack.seqj", "hero.achx"]);
        File.Exists(boss).ShouldBeTrue();
        Directory.GetFiles(_dir.Path, "*.tmp").ShouldBeEmpty();
        outcome.Message(plan).ShouldBe("Converted 1 to .achj; updated 1 sequence file.");
    }

    [Fact]
    public void Run_ASequenceThatCouldNotBeUpdated_KeepsTheAchxItPlays()
    {
        string hero = Achx("hero.achx");
        string boss = Achx("boss.achx");
        string attack = Seqj("attack.seqj", "hero.achx");
        Seqj("boss.seqj", "boss.achx");
        string before = File.ReadAllText(attack);

        var outcome = AchxConversion.Run(Plan(hero, boss), Steps(path => path == attack ? "in use" : Recycle(path)));

        File.Exists(hero).ShouldBeTrue();
        File.ReadAllText(attack).ShouldBe(before);
        outcome.Converted.ShouldBe([boss]);
        outcome.Differences.ShouldBe(["attack.seqj: not updated (in use). hero.achx is kept beside the .achj. To finish, delete the .achj and convert again"]);
        Directory.GetFiles(_dir.Path, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Run_ASequenceThePlanSaysCannotBeUpdated_IsNotTouched_AndIsNoDifference()
    {
        string hero = Achx("hero.achx");
        string locked = Seqj("locked.seqj", "hero.achx");
        File.SetAttributes(locked, FileAttributes.ReadOnly);

        var outcome = AchxConversion.Run(Plan(hero), Steps());

        outcome.Differences.ShouldBeEmpty();
        File.Exists(hero).ShouldBeFalse();
        SequenceListSave.FromFile(locked).Sources.ShouldBe(["hero.achx"]);
    }

    [Fact]
    public void Run_WhatChangedSinceThePlan_IsADifference()
    {
        string hero = Achx("hero.achx");
        string taken = Achx("taken.achx");
        string refused = Achx("refused.achx");
        string unsaved = Achx("unsaved.achx");
        string lied = Achx("lied.achx");
        string edited = Achx("edited.achx");
        var plan = AchxConversionPlan.Build([hero, taken, refused, unsaved, lied, edited], _dir.Path, OpenDocuments.None with
        {
            Animations = new Dictionary<FilePath, (bool, string?)> { [new FilePath(unsaved)] = (true, null) },
        });
        File.WriteAllText(At("taken.achj"), "another file");
        Seqj("late.seqj", "hero.achx", "refused.achx");
        File.WriteAllText(edited, File.ReadAllText(edited).Replace("Walk", "Run"));

        var outcome = AchxConversion.Run(plan, Steps(path => path == lied ? null : Recycle(path), _ => "disk full"));

        outcome.Differences.ShouldBe(
        [
            "taken.achx: not converted, taken.achj already exists",
            "unsaved.achx: not converted, its unsaved edits could not be saved (disk full)",
            "edited.achx: not converted, it is not the file the plan is for any more",
            "late.seqj: plays hero.achx and refused.achx since the plan. hero.achx and refused.achx are kept beside the .achj. To finish, delete the .achj and convert again",
            "lied.achx: converted, but the original could not be recycled (it is still there)",
        ]);
        outcome.Message(plan).ShouldStartWith("⚠ Converted 0, 5 problems: taken.achx: ");
        (File.Exists(hero), File.Exists(refused)).ShouldBe((true, true));
        File.ReadAllText(At("taken.achj")).ShouldBe("another file");
    }

    // The converted file is gone, so undo must not bring its name back, and must still undo the edit.
    [Fact]
    public void ChangeSourceExtension_KeepsMemoryFileAndHistoryOnTheNewFile()
    {
        string hero = Achx("hero.achx");
        string attack = Seqj("attack.seqj", "hero.achx");
        var document = SequenceDocument.Load(attack);
        var undo = new UndoManager();
        document.SetChainStart(undo, 0, 0, 0, 5);
        AchxToAchjConverter.Convert(hero);

        document.ChangeSourceExtension([hero], ".achj", (save, path) => AchxConversion.ReplaceRecyclingOriginal(save, path, Recycle)).ShouldBeNull();
        File.Delete(hero);

        void ShouldPlay(double start)
        {
            document.Save.Sources.ShouldBe(["hero.achj"]);
            document.Save.Sequences[0].Tracks[0].Chains.Single().ShouldSatisfyAllConditions(
                block => block.Source.ShouldBe("hero.achj"), block => block.Start.ShouldBe(start));
            document.IsSourceMissing("hero.achj").ShouldBeFalse();
            File.ReadAllText(attack).ReplaceLineEndings().ShouldBe(document.Save.ToJsonString().ReplaceLineEndings());
        }
        ShouldPlay(5);
        undo.UndoHistory.Count().ShouldBe(1);
        undo.Undo();
        ShouldPlay(0);
        undo.Redo();
        ShouldPlay(5);
        document.ReloadIfChangedOnDisk().Changed.ShouldBeFalse("the document's own write is no outside change");
    }

    [Fact]
    public void ChangeSourceExtension_WriteFails_ChangesNothing()
    {
        string hero = Achx("hero.achx");
        var document = SequenceDocument.Load(Seqj("attack.seqj", "hero.achx"));

        document.ChangeSourceExtension([hero], ".achj", (_, _) => "in use").ShouldBe("in use");

        document.Save.Sources.ShouldBe(["hero.achx"]);
        document.HasUnsavedEdits.ShouldBeFalse();
    }

    [Fact]
    public void ChangeExtension_TheLibraryListsTheNewFileAlready_ListsItOnce()
    {
        string hero = Achx("hero.achx");
        var save = SequenceListSave.FromFile(Seqj("attack.seqj", "hero.achx", "hero.achj"));

        SequenceSourcePaths.ChangeExtension(save, new FilePath(_dir.Path), [hero], ".achj");

        save.Sources.ShouldBe(["hero.achj"]);
        save.Sequences[0].Tracks[0].Chains.ShouldAllBe(block => block.Source == "hero.achj");
    }

    [Fact]
    public void FirstDifference_SaysWhereAListDiffers()
    {
        AnimationChainListSave List(float secondLength, string eventName = "Hit", float radius = 4) => new()
        {
            AnimationChains =
            {
                new AnimationChainSave { Name = "Idle" },
                new AnimationChainSave
                {
                    Name = "Walk",
                    Frames =
                    {
                        new AnimationFrameSave
                        {
                            Events = { new FlatRedBall2.Animation.AnimationFrameEvent { Name = eventName } },
                            ShapesSave = WithCircle(radius),
                        },
                        new AnimationFrameSave { FrameLength = secondLength },
                    },
                },
            },
        };

        AnimationListComparison.FirstDifference(List(0.1f), List(0.1f)).ShouldBeNull();
        AnimationListComparison.FirstDifference(List(0.1f), List(0.2f)).ShouldBe("animation 'Walk', frame 2");
        AnimationListComparison.FirstDifference(List(0.1f), List(0.1f, eventName: "Miss")).ShouldBe("animation 'Walk', frame 1");
        AnimationListComparison.FirstDifference(List(0.1f), List(0.1f, radius: 5)).ShouldBe("animation 'Walk', frame 1");
    }

    private static ShapesSave WithCircle(float radius)
    {
        var shapes = new ShapesSave();
        shapes.Add(new CircleSave { Name = "Body", Radius = radius });
        return shapes;
    }

    // What an old .achx may hold: an empty shape list, FRB1 shape fields, UV and milliseconds, names twice.
    [Fact]
    public void Build_ARealWorldFile_ComparesEqual()
    {
        File.WriteAllText(At("old.achx"), """
            <?xml version="1.0"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Millisecond</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
                <Frame>
                  <TextureName>a.png</TextureName>
                  <FlipHorizontal>true</FlipHorizontal>
                  <FlipVertical>false</FlipVertical>
                  <FrameLength>100.5</FrameLength>
                  <RelativeX>0</RelativeX>
                  <LeftCoordinate>0.333333343</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
                  <ShapeCollectionSave><AxisAlignedRectangleSaves /><AxisAlignedCubeSaves /><PolygonSaves /><CircleSaves /><SphereSaves /></ShapeCollectionSave>
                </Frame>
                <Frame>
                  <FrameLength>1</FrameLength>
                  <ShapeCollectionSave>
                    <AxisAlignedRectangleSaves><AxisAlignedRectangleSave><X>1</X><Y>2</Y><Z>3</Z><ScaleX>4</ScaleX><ScaleY>5</ScaleY><Name>Hit</Name><Alpha>0.5</Alpha><Red>1</Red><Green>0.25</Green><Blue>1</Blue></AxisAlignedRectangleSave></AxisAlignedRectangleSaves>
                  </ShapeCollectionSave>
                </Frame>
              </AnimationChain>
              <AnimationChain><Name>Walk</Name></AnimationChain>
              <ProjectFile>Game.gluj</ProjectFile>
            </AnimationChainArraySave>
            """);

        Plan(At("old.achx")).Files.Single().Problem.ShouldBeNull();
    }

    // Cubes and spheres have no place in an .achj.
    [Theory]
    [InlineData("<Custom>5</Custom>", "", "<Custom> would be lost")]
    [InlineData("", "<Custom><Part>5</Part></Custom>", "<Custom> would be lost")]
    [InlineData("", "<ShapeCollectionSave><CircleSaves><CircleSave><Radius>2</Radius><Custom>5</Custom></CircleSave></CircleSaves></ShapeCollectionSave>", "<Custom> would be lost")]
    [InlineData("", "<Tagged by=\"me\" />", "<Tagged> would be lost")]
    [InlineData("", "<RelativeX by=\"me\">4</RelativeX>", "the attribute by would be lost")]
    [InlineData("", "<ShapeCollectionSave><SphereSaves><SphereSave><Radius>2</Radius></SphereSave></SphereSaves></ShapeCollectionSave>", "Not exact: animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><AxisAlignedCubeSaves><AxisAlignedCubeSave><X>1</X></AxisAlignedCubeSave></AxisAlignedCubeSaves></ShapeCollectionSave>", "Not exact: animation 'Walk', frame 1")]
    [InlineData("<ProjectFile></ProjectFile><Custom />", "<FlipVertical>false</FlipVertical><RelativeY>0</RelativeY><Events />", null)]
    public void Build_AnElementThatIsNotRead_IsLeftAlone(string inList, string inFrame, string? problem)
    {
        File.WriteAllText(At("a.achx"), $"<AnimationChainArraySave>{inList}<AnimationChain><Name>Walk</Name><Frame><TextureName>a.png</TextureName>{inFrame}<FrameLength>1</FrameLength></Frame></AnimationChain></AnimationChainArraySave>");

        Plan(At("a.achx")).Files.Single().Problem?.Why.ShouldBe(problem);
        Plan(At("a.achx")).Files.Single().Converts.ShouldBe(problem is null);
    }

    [Fact]
    public void Build_AFileWithComments_SaysTheyAreNotKept()
    {
        string hero = Achx("hero.achx");
        File.WriteAllText(hero, File.ReadAllText(hero).Replace("<Name>", "<!-- note --><Name>"));

        var view = ConversionPlanView.Of(Plan(hero));

        view.Animations.Items.Single().Note.ShouldBe("comments are not kept");
        view.Animations.GroupSummary.ShouldBe("1 animation · 1 frame · 1 loses comments");
    }

    [Fact]
    public void SameAs_IsTrueUntilWhatWouldRunDiffers()
    {
        string hero = Achx("hero.achx");
        string boss = Achx("boss.achx");
        string attack = Seqj("attack.seqj", "hero.achx");
        var plan = Plan(hero, boss);
        bool Same() => AchxConversionPlan.Build([hero, boss], _dir.Path, OpenDocuments.None, plan).SameAs(plan);
        void Changes(string path, string from, string to)
        {
            string text = File.ReadAllText(path);
            File.WriteAllText(path, text.Replace(from, to));
            Same().ShouldBeFalse($"{Path.GetFileName(path)}: {from} to {to}");
            File.WriteAllText(path, text);
        }

        Same().ShouldBeTrue();
        Changes(hero, "Walk", "Run");
        Changes(hero, "<Name>Walk</Name>", "");
        Changes(attack, "\"source\": \"hero.achx\"", "\"source\": \"gone.achx\"");
        Changes(attack, "\"hero.achx\"\r\n  ]".Replace("\r\n", Environment.NewLine), "\"hero.achx\", \"boss.achx\"]");
        Seqj("late.seqj", "boss.achx");
        Same().ShouldBeFalse("a sequence appeared");
        File.Delete(At("late.seqj"));
        File.WriteAllText(At("boss.achj"), "{}");
        Same().ShouldBeFalse("the .achj appeared");
    }

    [Fact]
    public void Run_TheRecycleBinTakesNothingButSaysSo_LeavesTheSequenceAsItWas()
    {
        string hero = Achx("hero.achx");
        string attack = Seqj("attack.seqj", "hero.achx");
        string before = File.ReadAllText(attack);

        var outcome = AchxConversion.Run(Plan(hero), Steps(_ => null));

        File.ReadAllText(attack).ShouldBe(before);
        File.Exists(hero).ShouldBeTrue();
        outcome.Differences.Single().ShouldStartWith("attack.seqj: not updated (it is still there). hero.achx is kept");
        Directory.GetFiles(_dir.Path, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Run_TheUpdatedSequenceCannotTakeItsPlace_IsKept_AndTheMessageSaysWhereBothAre()
    {
        string hero = Achx("hero.achx");
        string attack = Seqj("attack.seqj", "hero.achx");

        var outcome = AchxConversion.Run(Plan(hero), Steps(path =>
        {
            Recycle(path);
            if (path == attack) Directory.CreateDirectory(path);
            return null;
        }));

        string updated = Directory.GetFiles(_dir.Path, "attack.seqj.*.tmp").Single();
        SequenceListSave.FromFile(updated).Sources.ShouldBe(["hero.achj"]);
        outcome.Differences.Single().ShouldContain($"The original is in the Recycle Bin and the updated one is {Path.GetFileName(updated)}");
        File.Exists(hero).ShouldBeTrue();
    }

    [Fact]
    public void View_HasACardPerKind_WithTheProblemsApart()
    {
        string hero = Achx("Sprites/hero.achx");
        Achx("Sprites/taken.achx");
        File.WriteAllText(At("Sprites/taken.achj"), "another file");
        Seqj("attack.seqj", "Sprites/hero.achx");
        File.WriteAllText(At("damaged.seqj"), "{");
        File.SetAttributes(Seqj("locked.seqj", "Sprites/hero.achx"), FileAttributes.ReadOnly);

        var view = ConversionPlanView.Of(Plan(hero, At("Sprites/taken.achx")));

        view.CanConvert.ShouldBeTrue();
        view.Animations.Issues.ShouldBe([new ConversionIssue(ConversionMark.LeftAlone, "Sprites/", "taken.achx", "taken.achj exists", "")]);
        view.Animations.Items.ShouldBe([new ConversionItem("Sprites/", "hero.achx", "hero.achj", "", "1 animation", "1 frame")]);
        (view.Animations.GroupTitle, view.Animations.GroupSummary).ShouldBe(("1 becomes .achj", "1 animation · 1 frame"));
        view.Sequences!.Issues.Select(issue => (issue.Mark, issue.Name, issue.Why)).ShouldBe(
        [
            (ConversionMark.StopsPlaying, "locked.seqj", "Read-only · 1 block stops playing"),
            (ConversionMark.Unknown, "damaged.seqj", "Can't be read · Unknown"),
        ]);
        view.Sequences.Items.ShouldBe([new ConversionItem("", "attack.seqj", "", "", "1 block", "")]);
        view.Facts.Select(fact => fact.Label).ShouldBe(["Check", "Originals", "Looks in", "Unchanged"]);
        view.Facts[2].Value.ShouldBe(_dir.Path);
    }

    [Fact]
    public void View_NothingConverts_HasNoSequencesAndNothingToAgreeTo()
    {
        string taken = Achx("taken.achx");
        File.WriteAllText(At("taken.achj"), "another file");
        Seqj("attack.seqj", "taken.achx");

        var view = ConversionPlanView.Of(Plan(taken));

        view.CanConvert.ShouldBeFalse();
        view.Sequences.ShouldBeNull();
        view.Facts.ShouldBeEmpty();
    }
}
