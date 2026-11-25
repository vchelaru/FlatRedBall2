using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>Undo puts back the document and the file exactly as they were before the edit.</summary>
[Collection("SequentialSingletons")]
public class UndoFidelityTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private string _path = "";

    public void Dispose() => _dir.Dispose();

    private List<AnimationChainSave> Chains => _ctx.ProjectManager.AnimationChainListSave!.AnimationChains;

    private string OnDisk => File.ReadAllText(_path);

    private void Open(DocType type)
    {
        _path = Documents.Write(_dir.Path, type, "front");
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(_path));
        _ctx.AppCommands.SaveCurrentAnimationChainList();
    }

    [Fact]
    public void DeleteFrames_PickedLastFirst_UndoPutsThemBackInOrder()
    {
        var chain = TestHelpers.MakeChain(_ctx.Acls, "Walk", 4);
        var before = chain.Frames.ToList();

        _ctx.AppCommands.DeleteFrames([before[2], before[0]]);
        _ctx.UndoManager.Undo();

        chain.Frames.Select(f => f.TextureName).ShouldBe(before.Select(f => f.TextureName));
    }

    [Fact]
    public void DeleteAnimationChains_PickedLastFirst_UndoPutsThemBackInOrder()
    {
        foreach (var name in new[] { "A", "B", "C", "D" }) TestHelpers.MakeChain(_ctx.Acls, name, 1);
        var before = _ctx.Acls.AnimationChains.ToList();

        _ctx.AppCommands.DeleteAnimationChains([before[2], before[0]]);
        _ctx.UndoManager.Undo();

        _ctx.Acls.AnimationChains.Select(c => c.Name).ShouldBe(["A", "B", "C", "D"]);
    }

    [Fact]
    public void PasteFramesCut_PickedLastFirst_UndoPutsThemBackInOrder()
    {
        var chain = TestHelpers.MakeChain(_ctx.Acls, "Walk", 4);
        var target = TestHelpers.MakeChain(_ctx.Acls, "Run", 1);
        var before = chain.Frames.ToList();

        _ctx.AppCommands.PasteFramesCut(target, [before[2], before[0]], null);
        _ctx.UndoManager.Undo();

        chain.Frames.Select(f => f.TextureName).ShouldBe(before.Select(f => f.TextureName));
    }

    [Theory]
    [InlineData(DocType.Achx)]
    [InlineData(DocType.Achj)]
    public void PasteShapes_OntoAFrameWithoutShapes_UndoLeavesTheFileAsItWas(DocType type)
    {
        Open(type);
        var frame = Chains[1].Frames[0];
        frame.ShapesSave.ShouldBeNull();
        string before = OnDisk;

        _ctx.AppCommands.PasteShapes(frame, [new CircleSave { Name = "C", Radius = 2 }]);
        _ctx.UndoManager.Undo();

        OnDisk.ShouldBe(before);
    }

    [Fact]
    public void SetFrameTextureName_ToTheSameTexture_RecordsNoUndoEntry()
    {
        Open(DocType.Achx);

        _ctx.AppCommands.SetAllFramesTextureName(Chains[0], Chains[0].Frames[0].TextureName);

        _ctx.UndoManager.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void FlipChain_WithNoFrames_RecordsNoUndoEntry()
    {
        var chain = TestHelpers.MakeChain(_ctx.Acls, "Empty");

        _ctx.AppCommands.FlipChainHorizontally(chain);

        _ctx.UndoManager.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void MatchCircleToFrame_AlreadyMatching_RecordsNoUndoEntry()
    {
        var frame = TestHelpers.MakeChain(_ctx.Acls, "Walk", 1).Frames[0];
        var circle = new CircleSave { Name = "C", Radius = 2 };
        frame.ShapesSave!.Add(circle);

        _ctx.AppCommands.MatchCircleToFrame(circle, frame);

        _ctx.UndoManager.UndoHistory.ShouldBeEmpty();
    }

    [Fact]
    public void PasteShapesCut_OfAShapeNoLongerInTheDocument_LeavesTheTargetWithoutAShapeList()
    {
        var frame = TestHelpers.MakeChain(_ctx.Acls, "Walk", 1).Frames[0];
        frame.ShapesSave = null;

        _ctx.AppCommands.PasteShapesCut(frame, [new CircleSave { Name = "C" }]);

        frame.ShapesSave.ShouldBeNull();
    }

    [Fact]
    public void SetFrameRelative_UpThenBackDown_KeepsOnlyTheEarlierUndoEntry()
    {
        var frame = TestHelpers.MakeChain(_ctx.Acls, "Walk", 1).Frames[0];
        _ctx.AppCommands.SetFrameRelative([frame], 5f, 0f);
        _ctx.AppCommands.SealPendingEdits();

        _ctx.AppCommands.SetFrameRelative([frame], 6f, 0f);
        _ctx.AppCommands.SetFrameRelative([frame], 5f, 0f);
        _ctx.UndoManager.UndoHistory.Count.ShouldBe(1);

        // The run that ended must not fold a later edit into the earlier entry.
        _ctx.AppCommands.SetFrameRelative([frame], 7f, 0f);
        _ctx.UndoManager.Undo();
        frame.RelativeX.ShouldBe(5f);
    }

    [Fact]
    public void ShapeFields_UpThenBackDown_RecordNoUndoEntry()
    {
        var frame = TestHelpers.MakeChain(_ctx.Acls, "Walk", 1).Frames[0];
        var circle = new CircleSave { Name = "C", Radius = 2 };
        var polygon = new PolygonSave { Name = "P", Points = [new() { X = 0, Y = 0 }, new() { X = 1, Y = 0 }, new() { X = 0, Y = 1 }] };
        frame.ShapesSave!.Add(circle);
        frame.ShapesSave.Add(polygon);

        foreach (var x in new[] { 1f, 0f }) _ctx.AppCommands.SetCircleProps(frame, circle, "C", x, 0f, 2f);
        foreach (var x in new[] { 1f, 0f }) _ctx.AppCommands.SetCirclePropsBulk([circle], null, x, null, null);
        foreach (var x in new[] { 2f, 1f }) _ctx.AppCommands.MovePolygonVertex(polygon, 1, x, 0f);

        _ctx.UndoManager.UndoHistory.ShouldBeEmpty();
    }
}
