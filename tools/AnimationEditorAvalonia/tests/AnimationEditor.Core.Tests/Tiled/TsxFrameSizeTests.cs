using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// The frames of a <c>.tsx</c> animation are all one size, so a frame of another size is refused where it would be
/// added and stays where it is (#2147). Blink is 16x16 (tiles 5, 6), Torch is 32x16 (tiles 8, 10).
/// </summary>
[Collection("SequentialSingletons")]
public class TsxFrameSizeTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private AnimationChainSave Chain(string name) => _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(c => c.Name == name);

    private string Open()
    {
        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="5">
              <properties>
               <property name="Name" value="Blink"/>
              </properties>
              <animation>
               <frame tileid="5" duration="100"/>
               <frame tileid="6" duration="100"/>
              </animation>
             </tile>
             <tile id="8">
              <properties>
               <property name="Name" value="Torch"/>
              </properties>
              <animation>
               <frame tileid="8" duration="100"/>
               <frame tileid="10" duration="100"/>
              </animation>
             </tile>
             <tile id="9">
              <properties>
               <property name="ParentId" type="int" value="8"/>
              </properties>
              <animation>
               <frame tileid="9" duration="100"/>
               <frame tileid="11" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        FrameRegion.Of(Chain("Torch").Frames[1]).ShouldBe(new FrameRegion(32, 32, 64, 48));
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
        return path;
    }

    private void Add(string how, AnimationChainSave source, AnimationChainSave target)
    {
        var frame = source.Frames[0];
        switch (how)
        {
            case "paste": _ctx.AppCommands.PasteFrames(target, [frame], 1); break;
            case "cut": _ctx.AppCommands.PasteFramesCut(target, [frame], 1); break;
            case "drag": _ctx.AppCommands.MoveFrames([frame], source, target, 1); break;
            default:
                var r = FrameRegion.Of(frame);
                _ctx.AppCommands.AddFrameFromPixelBounds(target, "Heroes.png", (int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom);
                break;
        }
    }

    [Theory]
    [InlineData("paste", "Blink", "Torch"), InlineData("paste", "Torch", "Blink")]
    [InlineData("cut", "Blink", "Torch"), InlineData("cut", "Torch", "Blink")]
    [InlineData("drag", "Blink", "Torch"), InlineData("drag", "Torch", "Blink")]
    [InlineData("region", "Blink", "Torch"), InlineData("region", "Torch", "Blink")]
    public void FrameOfAnotherSize_IsRefused_AndEverythingStays(string how, string from, string to)
    {
        var path = Open();
        var before = File.ReadAllText(path);
        var frames = Chain(from).Frames.Concat(Chain(to).Frames).ToArray();

        Add(how, Chain(from), Chain(to));

        Chain(from).Frames.Concat(Chain(to).Frames).ShouldBe(frames);
        _ctx.UndoManager.CanUndo.ShouldBeFalse();
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
        File.ReadAllText(path).ShouldBe(before);
    }

    [Theory]
    [InlineData("paste"), InlineData("cut"), InlineData("drag"), InlineData("region")]
    public void FrameOfTheSameSize_IsAdded_AndUndoAndRedoSave(string how)
    {
        Open();
        _ctx.AppCommands.AddFrameFromPixelBounds(_ctx.AppCommands.AddAnimationChainWithName("Spark")!, "Heroes.png", 48, 48, 64, 64);

        Add(how, Chain("Blink"), Chain("Spark"));

        Chain("Spark").Frames.Count.ShouldBe(2);
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
        _ctx.UndoManager.Undo();
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
        _ctx.UndoManager.Redo();
        Chain("Spark").Frames.Count.ShouldBe(2);
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("paste"), InlineData("cut")]
    public void FramesOfTwoSizes_IntoAnEmptyAnimation_AreRefused(string how)
    {
        Open();
        var empty = _ctx.AppCommands.AddAnimationChainWithName("Spark")!;
        AnimationFrameSave[] frames = [Chain("Blink").Frames[0], Chain("Torch").Frames[0]];

        if (how == "paste") _ctx.AppCommands.PasteFrames(empty, frames);
        else _ctx.AppCommands.PasteFramesCut(empty, frames, null);

        empty.Frames.ShouldBeEmpty();
        Chain("Blink").Frames.Count.ShouldBe(2);
        Chain("Torch").Frames.Count.ShouldBe(2);
    }

    [Fact]
    public void InAnAchx_FramesOfAnySizeMove()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new() { Name = "Blink", Frames = { new() { RightCoordinate = 16, BottomCoordinate = 16 } } });
        acls.AnimationChains.Add(new() { Name = "Torch", Frames = { new() { RightCoordinate = 32, BottomCoordinate = 16 } } });
        _ctx.ProjectManager.AnimationChainListSave = acls;

        _ctx.AppCommands.MoveFrames([Chain("Blink").Frames[0]], Chain("Blink"), Chain("Torch"), 0);

        Chain("Torch").Frames.Count.ShouldBe(2);
    }

    /// <summary>Each of these copies a frame of its own animation, or resizes all of them.</summary>
    [Theory]
    [InlineData("duplicate frame"), InlineData("duplicate selection"), InlineData("duplicate chain")]
    [InlineData("add frame"), InlineData("add multiple"), InlineData("inspector")]
    public void OtherCommands_KeepTheFramesOfAnAnimationOneSize(string how)
    {
        Open();
        var torch = Chain("Torch");
        var commands = _ctx.AppCommands;

        switch (how)
        {
            case "duplicate frame": commands.DuplicateFrame(torch.Frames[0], torch); break;
            case "duplicate selection":
                commands.DuplicateSelection(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = [torch.Frames[0], Chain("Blink").Frames[0]] });
                break;
            case "duplicate chain": commands.DuplicateChain(torch); break;
            case "add frame": commands.AddFrame(torch); break;
            case "add multiple": commands.AddMultipleFrames(torch, 2, incrementUV: false); break;
            default: commands.SetFramePixelRegion([torch.Frames[0]], null, null, 16m, null); break;
        }

        _ctx.UndoManager.CanUndo.ShouldBeTrue();
        foreach (var chain in _ctx.ProjectManager.AnimationChainListSave!.AnimationChains)
            chain.Frames.Select(f => (f.RightCoordinate - f.LeftCoordinate, f.BottomCoordinate - f.TopCoordinate)).Distinct().Count().ShouldBe(1, chain.Name);
    }
}
