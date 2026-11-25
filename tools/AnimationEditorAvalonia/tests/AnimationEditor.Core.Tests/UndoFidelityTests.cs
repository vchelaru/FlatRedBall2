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

        _ctx.AppCommands.PasteFramesCut(target, [before[2], before[0]], null, [before[2], before[0]]);
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
    public void PasteShapesCut_GivenUp_LeavesTheTargetWithoutAShapeList()
    {
        var frame = TestHelpers.MakeChain(_ctx.Acls, "Walk", 1).Frames[0];
        frame.ShapesSave = null;

        _ctx.AppCommands.PasteShapesCut(frame, [new CircleSave { Name = "C" }], sourcesToRemove: [], frame);

        frame.ShapesSave.ShouldBeNull();
    }
}
