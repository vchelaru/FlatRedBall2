using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Tiled;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// No edit command leaves a clean <c>.tsx</c> holding something the file cannot store (#2009). A command added to
/// <see cref="IAppCommands"/> fails here until it is in <see cref="Commands"/> or in one of the lists below it.
/// </summary>
[Collection("SequentialSingletons")]
public class TsxEditCommandsStayStorableTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private const string TwoAnimations = """
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <tile id="4">
          <animation>
           <frame tileid="4" duration="200"/>
          </animation>
         </tile>
        """;

    private string OpenTsx(string tiles, string gridAttributes = "", int imageSize = 64)
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" {gridAttributes} tilecount="16" columns="4">
             <image source="Heroes.png" width="{imageSize}" height="{imageSize}"/>
            {tiles}
            </tileset>
            """);
        WritePngHeader("Heroes.png", imageSize);
        WritePngHeader("other.png", 64);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        return path;
    }

    private void WritePngHeader(string name, int size)
    {
        var bytes = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), size);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), size);
        File.WriteAllBytes(Path.Combine(_dir.Path, name), bytes);
    }

    private static List<string> Unstorable(TestServices ctx)
    {
        var acls = ctx.ProjectManager.AnimationChainListSave!;
        var found = acls.AnimationChains
            .SelectMany(c => TsxLossyDataCheck.DroppedKinds(c, acls.TimeMeasurementUnit)).Select(k => k.ToString()).ToList();
        found.AddRange(acls.AnimationChains.SelectMany(c => c.Frames)
            .Where(f => f.TextureName != "Heroes.png").Select(f => $"texture '{f.TextureName}'"));
        return found;
    }

    [Theory]
    [MemberData(nameof(CommandRows))]
    public void EditCommand_OnACleanTsx_LeavesNothingTheFileCannotStore(string name, int variant)
    {
        OpenTsx(TwoAnimations);
        _ctx.SelectedState.SelectedChain = First(_ctx);
        Unstorable(_ctx).ShouldBeEmpty();

        Commands.Where(c => c.Name == name).ElementAt(variant).Run(_ctx);

        Unstorable(_ctx).ShouldBeEmpty();
    }

    [Fact]
    public void EveryIAppCommandsMethod_IsRunHereOrListedAsAddingNoData()
    {
        var known = Commands.Select(c => c.Name).Concat(AddsNoData).Concat(KeepsWhatItIsGiven).ToHashSet();
        var methods = typeof(IAppCommands).GetMethods().Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().ToList();

        methods.Where(m => !known.Contains(m)).ShouldBeEmpty("run it in Commands, or list why it adds no data");
        known.Where(k => !methods.Contains(k)).ShouldBeEmpty("no longer on IAppCommands");

        // A name is not enough for an overloaded command: each of these is run, or adds no data, on its own.
        typeof(IAppCommands).GetMethods().Where(m => !m.IsSpecialName).GroupBy(m => m.Name).Where(g => g.Count() > 1)
            .SelectMany(g => g).Select(Signature).OrderBy(s => s, StringComparer.Ordinal)
            .ShouldBe(Overloads);
    }

    private static string Signature(MethodInfo m) => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => TypeName(p.ParameterType)))})";

    private static string TypeName(Type type) =>
        type.IsGenericType ? $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>" : type.Name;

    private static readonly string[] Overloads =
    [
        "PasteShapes(AnimationFrameSave, IReadOnlyList<Object>)",
        "PasteShapes(IReadOnlyList<AnimationFrameSave>, IReadOnlyList<Object>)",
        "RefreshTreeNode(AnimationChainSave)",
        "RefreshTreeNode(AnimationFrameSave)",
        "SetFrameTextureName(AnimationFrameSave, String)",
        "SetFrameTextureName(IReadOnlyList<AnimationFrameSave>, String)",
    ];

    public static IEnumerable<object[]> CommandRows() =>
        Commands.GroupBy(c => c.Name).SelectMany(g => g.Select((_, i) => new object[] { g.Key, i }));

    private static AnimationChainSave First(TestServices ctx) => ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
    private static AnimationChainSave Second(TestServices ctx) => ctx.ProjectManager.AnimationChainListSave!.AnimationChains[1];
    private static AnimationFrameSave Frame(TestServices ctx) => First(ctx).Frames[0];
    private static AnimationFrameSave[] Frames(TestServices ctx) => First(ctx).Frames.ToArray();

    // Not in the document: a command given one with a frame must not attach it there.
    private static CircleSave Circle() => new() { Name = "Hit", Radius = 4 };
    private static AARectSave Rect() => new() { Name = "Box", ScaleX = 4, ScaleY = 4 };
    private static PolygonSave Polygon() => NewShapeDefaults.Polygon(new AnimationFrameSave(), "Poly");

    private static (string, Action<TestServices>) Row(string name, Action<TestServices> run) => (name, run);

    /// <summary>Each command, given what a <c>.tsx</c> cannot store wherever its arguments can carry it.</summary>
    private static readonly (string Name, Action<TestServices> Run)[] Commands =
    [
        Row(nameof(IAppCommands.AddAxisAlignedRectangle), c => c.AppCommands.AddAxisAlignedRectangle(Frame(c))),
        Row(nameof(IAppCommands.AddCircle), c => c.AppCommands.AddCircle(Frame(c))),
        Row(nameof(IAppCommands.AddPolygon), c => c.AppCommands.AddPolygon(Frame(c))),
        Row(nameof(IAppCommands.AddShape), c => c.AppCommands.AddShape(Frame(c), Circle())),
        Row(nameof(IAppCommands.DuplicateShape), c => c.AppCommands.DuplicateShape(Circle())),
        Row(nameof(IAppCommands.PasteShapes), c => c.AppCommands.PasteShapes(Frame(c), [Circle()])),
        Row(nameof(IAppCommands.PasteShapes), c => c.AppCommands.PasteShapes(Frames(c), [Circle()])),
        Row(nameof(IAppCommands.PasteShapesCut), c => c.AppCommands.PasteShapesCut(Frames(c)[1], [Circle()])),
        Row(nameof(IAppCommands.MatchRectangleToFrame), c => c.AppCommands.MatchRectangleToFrame(Rect(), Frame(c))),
        Row(nameof(IAppCommands.MatchCircleToFrame), c => c.AppCommands.MatchCircleToFrame(Circle(), Frame(c))),
        Row(nameof(IAppCommands.MatchRectanglesToFrames), c => c.AppCommands.MatchRectanglesToFrames([Rect()])),
        Row(nameof(IAppCommands.SetRectProps), c => c.AppCommands.SetRectProps(Frame(c), Rect(), "Box", 1, 2, 3, 4)),
        Row(nameof(IAppCommands.SetCircleProps), c => c.AppCommands.SetCircleProps(Frame(c), Circle(), "Hit", 1, 2, 3)),
        Row(nameof(IAppCommands.SetPolygonProps), c => c.AppCommands.SetPolygonProps(Frame(c), Polygon(), "Poly", 1, 2)),

        Row(nameof(IAppCommands.SetFrameFlip), c => c.AppCommands.SetFrameFlip(Frames(c), true, true, true)),
        Row(nameof(IAppCommands.FlipChainHorizontally), c => c.AppCommands.FlipChainHorizontally(First(c))),
        Row(nameof(IAppCommands.FlipChainVertically), c => c.AppCommands.FlipChainVertically(First(c))),
        Row(nameof(IAppCommands.FlipChainDiagonally), c => c.AppCommands.FlipChainDiagonally(First(c))),
        Row(nameof(IAppCommands.DuplicateChain), c => c.AppCommands.DuplicateChain(First(c), ChainFlip.Horizontal)),
        Row(nameof(IAppCommands.DuplicateChains), c => c.AppCommands.DuplicateChains([First(c), Second(c)], ChainFlip.Vertical | ChainFlip.Diagonal)),
        Row(nameof(IAppCommands.DuplicateFrame), c => c.AppCommands.DuplicateFrame(Frame(c), First(c))),
        Row(nameof(IAppCommands.DuplicateSelection), c => c.AppCommands.DuplicateSelection(
            new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = Frames(c) })),

        Row(nameof(IAppCommands.SetChainLoop), c => c.AppCommands.SetChainLoop(First(c), false)),
        Row(nameof(IAppCommands.SetChainsLoop), c => c.AppCommands.SetChainsLoop([First(c)], false)),
        Row(nameof(IAppCommands.SetChainsPingPong), c => c.AppCommands.SetChainsPingPong([First(c)], true)),
        Row(nameof(IAppCommands.SetChainsReverse), c => c.AppCommands.SetChainsReverse([First(c)], true)),

        Row(nameof(IAppCommands.SetFrameRelative), c => c.AppCommands.SetFrameRelative(Frames(c), 3, 4)),
        Row(nameof(IAppCommands.AdjustOffsetsJustifyBottom), c => c.AppCommands.AdjustOffsetsJustifyBottom(First(c))),
        Row(nameof(IAppCommands.AdjustOffsetsAdjustAll), c => c.AppCommands.AdjustOffsetsAdjustAll(First(c), 3f, 4f, relative: true)),

        Row(nameof(IAppCommands.SetFrameColor), c => c.AppCommands.SetFrameColor(Frames(c), 10, 20, 30)),
        Row(nameof(IAppCommands.SetFrameColorOperation), c => c.AppCommands.SetFrameColorOperation(Frames(c), ColorOperation.Add)),
        Row(nameof(IAppCommands.SetFrameColorValues), c => c.AppCommands.SetFrameColorValues(Frames(c), 1, 2, 3, 4, ColorOperation.Add)),
        Row(nameof(IAppCommands.SetFrameAlpha), c => c.AppCommands.SetFrameAlpha(Frames(c), 10)),

        Row(nameof(IAppCommands.SetFrameKeyframe), c => c.AppCommands.SetFrameKeyframe(Frames(c), KeyframeGroup.Offset, false)),
        Row(nameof(IAppCommands.SetFrameKeyframeCurve), c => c.AppCommands.SetFrameKeyframeCurve(Frames(c), KeyframeGroup.Color, KeyframeCurveChoice.EaseIn)),
        Row(nameof(IAppCommands.SetShapeKeyframe), c => c.AppCommands.SetShapeKeyframe([Circle()], ShapeKeyframeGroup.Offset, false)),
        Row(nameof(IAppCommands.SetShapeKeyframeCurve), c => c.AppCommands.SetShapeKeyframeCurve([Circle()], ShapeKeyframeGroup.Offset, KeyframeCurveChoice.EaseIn)),

        Row(nameof(IAppCommands.AddFrameEvent), c => c.AppCommands.AddFrameEvent(Frame(c), "Step")),
        Row(nameof(IAppCommands.SetFrameEvent), c => c.AppCommands.SetFrameEvent(Frame(c), 0, "Step", null)),

        Row(nameof(IAppCommands.AddFrame), c => c.AppCommands.AddFrame(First(c))),
        Row(nameof(IAppCommands.AddFrame), c => c.AppCommands.AddFrame(First(c), "other.png")),
        Row(nameof(IAppCommands.AddFrameFromPixelBounds), c => c.AppCommands.AddFrameFromPixelBounds(First(c), "other.png", 16, 0, 32, 16)),
        Row(nameof(IAppCommands.AddMultipleFrames), c => c.AppCommands.AddMultipleFrames(First(c), 2, incrementUV: true)),
        Row(nameof(IAppCommands.SetFrameTextureName), c => c.AppCommands.SetFrameTextureName(Frame(c), "other.png")),
        Row(nameof(IAppCommands.SetFrameTextureName), c => c.AppCommands.SetFrameTextureName(Frame(c), null)),
        Row(nameof(IAppCommands.SetFrameTextureName), c => c.AppCommands.SetFrameTextureName(Frames(c), "other.png")),
        Row(nameof(IAppCommands.SetFrameTextureName), c => c.AppCommands.SetFrameTextureName(Frames(c), "")),
        Row(nameof(IAppCommands.SetAllFramesTextureName), c => c.AppCommands.SetAllFramesTextureName(First(c), "other.png")),

        Row(nameof(IAppCommands.SetFrameLength), c => c.AppCommands.SetFrameLength(Frames(c), 0.001m)),
        Row(nameof(IAppCommands.SetAllFrameLengths), c => c.AppCommands.SetAllFrameLengths(First(c), 0.001f)),
        Row(nameof(IAppCommands.ScaleFrameTimesProportional), c => c.AppCommands.ScaleFrameTimesProportional(First(c), 2f)),
        Row(nameof(IAppCommands.ScaleFrameTimesSetAllSame), c => c.AppCommands.ScaleFrameTimesSetAllSame(First(c), 2f)),
        Row(nameof(IAppCommands.SetTimeMeasurementUnit), c => c.AppCommands.SetTimeMeasurementUnit(TimeMeasurementUnit.Millisecond)),
        Row(nameof(IAppCommands.SetFramePixelRegion), c => c.AppCommands.SetFramePixelRegion(Frames(c), 16, 16, 16, 16)),
    ];

    /// <summary>Commands that only remove, reorder, name, or edit shapes already there, and ones that edit no document.</summary>
    private static readonly string[] AddsNoData =
    [
        // Remove, clear, reorder or name.
        nameof(IAppCommands.DeleteAnimationChains), nameof(IAppCommands.DeleteCircle), nameof(IAppCommands.DeleteAxisAlignedRectangle),
        nameof(IAppCommands.DeleteShapes), nameof(IAppCommands.DeleteFrames), nameof(IAppCommands.DeleteSelection), nameof(IAppCommands.CutFromBackgroundTab),
        nameof(IAppCommands.RemoveFrameEvent), nameof(IAppCommands.ClearFrameColorValues),
        nameof(IAppCommands.AddAnimationChain), nameof(IAppCommands.AddAnimationChainWithName),
        nameof(IAppCommands.AddNewAnimationChain), nameof(IAppCommands.RenameChain), nameof(IAppCommands.SetChainLocked),
        nameof(IAppCommands.SetChainsLocked), nameof(IAppCommands.SetChainTsxOwnerTileId), nameof(IAppCommands.MoveChain),
        nameof(IAppCommands.MoveChainToIndex), nameof(IAppCommands.MoveChainsToIndex), nameof(IAppCommands.MoveChainToTop),
        nameof(IAppCommands.MoveChainToBottom), nameof(IAppCommands.MoveFrame), nameof(IAppCommands.MoveFrameToTop),
        nameof(IAppCommands.MoveFrameToBottom), nameof(IAppCommands.MoveFrames), nameof(IAppCommands.MoveFramesRelative),
        nameof(IAppCommands.MoveChainsRelative), nameof(IAppCommands.MoveShape), nameof(IAppCommands.MoveShapeToTop),
        nameof(IAppCommands.MoveShapeToBottom), nameof(IAppCommands.MoveShapeToIndex), nameof(IAppCommands.HandleReorder),
        nameof(IAppCommands.InvertFrameOrder), nameof(IAppCommands.SortAnimationsAlphabetically),
        // Change only the shapes they are given, and a clean .tsx has none.
        nameof(IAppCommands.SetPolygonGeometry), nameof(IAppCommands.MovePolygonVertex), nameof(IAppCommands.InsertPolygonVertex),
        nameof(IAppCommands.DeletePolygonVertex), nameof(IAppCommands.CommitPolygonPoints), nameof(IAppCommands.FlipPolygonHorizontally),
        nameof(IAppCommands.FlipPolygonVertically), nameof(IAppCommands.SetRectPropsBulk), nameof(IAppCommands.SetCirclePropsBulk),
        nameof(IAppCommands.SetShapePropsBulk), nameof(IAppCommands.SetPolygonPropsBulk),
        // Open, save, close, refresh and bookkeeping.
        nameof(IAppCommands.OpenAchxWorkflowAsync), nameof(IAppCommands.OpenTsxWorkflowAsync), nameof(IAppCommands.OpenProjectWorkflowAsync),
        nameof(IAppCommands.LoadAnimationChain), nameof(IAppCommands.CaptureTabEditorState), nameof(IAppCommands.RestoreTabSelection),
        nameof(IAppCommands.TryActivateTabFromCache), nameof(IAppCommands.ActivateTabContentAsync), nameof(IAppCommands.ReloadAchxFromDisk),
        nameof(IAppCommands.ReloadPngFromDisk), nameof(IAppCommands.WireHotReloadWatcher), nameof(IAppCommands.SyncHotReloadWatcher),
        nameof(IAppCommands.NewFile), nameof(IAppCommands.CloseProject), nameof(IAppCommands.HandleApplicationClosing),
        nameof(IAppCommands.SaveCurrentAnimationChainList), nameof(IAppCommands.SaveCurrentAnimationChainListAsync),
        nameof(IAppCommands.SaveDocument), nameof(IAppCommands.LastSaveStored), nameof(IAppCommands.ExportAsync),
        nameof(IAppCommands.RefreshTreeNode), nameof(IAppCommands.RefreshAnimationFrameDisplay), nameof(IAppCommands.RefreshWireframe),
        nameof(IAppCommands.RefreshTreeView), nameof(IAppCommands.GetTextureSize), nameof(IAppCommands.SealPendingEdits),
        nameof(IAppCommands.DiscardPendingEdits), nameof(IAppCommands.HasSameFrameNameCollision),
    ];

    /// <summary>
    /// A pasted chain or frame arrives as it was copied: the save says what the file did not keep
    /// (<see cref="TsxLossyDataCheck"/>), and a cut then keeps its source.
    /// </summary>
    private static readonly string[] KeepsWhatItIsGiven =
    [
        nameof(IAppCommands.PasteChains), nameof(IAppCommands.PasteFrames),
        nameof(IAppCommands.PasteChainsCut), nameof(IAppCommands.PasteFramesCut),
    ];

    [Theory]
    [InlineData("Heroes.png")]
    [InlineData("./sub/../Heroes.png")]
    public void SetFrameTextureName_TheTilesetsOwnImage_IsAllowedInAnySpelling(string spelling)
    {
        OpenTsx(TwoAnimations);
        foreach (var frame in Frames(_ctx)) frame.TextureName = "other.png"; // naming the image they already show is no edit

        _ctx.AppCommands.SetFrameTextureName(Frames(_ctx), spelling);
        _ctx.AppCommands.SetAllFramesTextureName(First(_ctx), Path.Combine(_dir.Path, "Heroes.png"));

        _ctx.UndoManager.UndoHistory.Count().ShouldBe(2);
    }

    /// <summary>Margin 2 and spacing 1 put tile 0 at (2,2), so the cell is not the sheet's corner.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Heroes.png")]
    public void AddFrame_FirstFrameOfAnEmptyTsx_IsTile0AndSavesOneAnimation(string? texture)
    {
        var path = OpenTsx("", "spacing=\"1\" margin=\"2\"", imageSize: 71);
        _ctx.AppCommands.CanvasDefaultTexturePath = () => "Heroes.png";
        var chain = _ctx.AppCommands.AddAnimationChainWithName("Walk")!;

        _ctx.AppCommands.AddFrame(chain, texture);

        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
        var saved = File.ReadAllText(path);
        saved.Split("<animation>").Length.ShouldBe(2, saved);
        FrameRegion.Of(chain.Frames.Single()).ShouldBe(new FrameRegion(2, 2, 18, 18));
        chain.Frames[0].TextureName.ShouldBe("Heroes.png");
    }

    /// <summary>A pasted frame keeps its image. The commands that continue from the last frame do not repeat it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddingFrames_AfterAPastedFrameOfAnotherImage_StartsAtTile0OfTheTileset(bool multiple)
    {
        OpenTsx(TwoAnimations);
        var chain = Second(_ctx);
        chain.Frames[0].TextureName = "other.png";

        if (multiple) _ctx.AppCommands.AddMultipleFrames(chain, 1, incrementUV: false);
        else _ctx.AppCommands.AddFrame(chain);

        chain.Frames.Count.ShouldBe(2);
        chain.Frames[1].TextureName.ShouldBe("Heroes.png");
        FrameRegion.Of(chain.Frames[1]).ShouldBe(new FrameRegion(0, 0, 16, 16));
    }

    [Fact]
    public void AddFrame_OfAnotherImage_AddsNoFrameAtAll()
    {
        OpenTsx(TwoAnimations);

        _ctx.AppCommands.AddFrame(First(_ctx), "other.png");

        First(_ctx).Frames.Count.ShouldBe(2);
        _ctx.UndoManager.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void AddFrame_OfTheTilesetsImage_ContinuesFromTheLastFramesCell()
    {
        OpenTsx(TwoAnimations);

        _ctx.AppCommands.AddFrame(First(_ctx), "Heroes.png");

        FrameRegion.Of(First(_ctx).Frames[2]).ShouldBe(new FrameRegion(16, 0, 32, 16));
    }

    [Fact]
    public void AddMultipleFrames_OnAnEmptyChainOfATsx_StartsAtTile0()
    {
        OpenTsx("");
        var chain = _ctx.AppCommands.AddAnimationChainWithName("Walk")!;

        _ctx.AppCommands.AddMultipleFrames(chain, 2, incrementUV: true);

        chain.Frames.Select(FrameRegion.Of).ShouldBe([new FrameRegion(0, 0, 16, 16), new FrameRegion(16, 0, 32, 16)]);
        Unstorable(_ctx).ShouldBeEmpty();
    }

    /// <summary>The same first frame on an even 4x4 grid: what an unguarded whole-sheet frame writes.</summary>
    [Fact]
    public void AddFrame_FirstFrameOfAnEmptyTsxWithoutMargin_AnimatesOnlyTile0()
    {
        var path = OpenTsx("");
        var chain = _ctx.AppCommands.AddAnimationChainWithName("Walk")!;

        _ctx.AppCommands.AddFrame(chain, "Heroes.png");
        _ctx.ProjectManager.SaveTsxProject();

        File.ReadAllText(path).Split("<animation>").Length.ShouldBe(2);
    }
}
