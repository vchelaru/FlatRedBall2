using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Undo/redo label conventions (issue #534): imperative tense, Title Case nouns,
/// singular omits the count, plural includes it.
/// </summary>
[Collection("SequentialSingletons")]
public class CommandDescriptionTests
{
    // ── AddCircleCommand / AddAxisAlignedRectangleCommand ─────────────────────

    [Fact]
    public void AddAxisAlignedRectangleCommand_Description_IncludesName()
    {
        var expected = "Add Rectangle 'Hitbox'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var rect = new AARectSave { Name = "Hitbox" };
        chain.Frames[0].ShapesSave = new ShapesSave();
        var cmd = new AddAxisAlignedRectangleCommand(
            rect, chain.Frames[0], ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void AddCircleCommand_Description_IncludesName()
    {
        var expected = "Add Circle 'Hurtbox'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var circle = new CircleSave { Name = "Hurtbox" };
        chain.Frames[0].ShapesSave = new ShapesSave();
        var cmd = new AddCircleCommand(
            circle, chain.Frames[0], ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    // ── BulkFrameRegionChangedCommand ─────────────────────────────────────────

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_MultiFrameDifferentDeltas_ReturnsEditWithCount()
    {
        var expected = "Edit 3 Frames";
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0.1f, AT: 0.1f, AR: 0.6f, AB: 0.6f),
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0.5f, BT: 0f, BR: 1.0f, BB: 0.5f,
                AL: 0.7f, AT: 0.2f, AR: 1.2f, AB: 0.7f), // different delta
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0.5f, BR: 0.5f, BB: 1.0f,
                AL: 0.1f, AT: 0.6f, AR: 0.6f, AB: 1.1f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_MultiFrameSameDelta_ReturnsMoveWithCount()
    {
        var expected = "Move 2 Frames";
        // Both frames move by (+0.1, +0.1) with same size — uniform drag
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0.1f, AT: 0.1f, AR: 0.6f, AB: 0.6f),
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0.5f, BT: 0f, BR: 1.0f, BB: 0.5f,
                AL: 0.6f, AT: 0.1f, AR: 1.1f, AB: 0.6f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_SingleFramePositionOnly_ReturnsMove()
    {
        var expected = "Move Frame";
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0.1f, AT: 0.1f, AR: 0.6f, AB: 0.6f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_SingleFrameSizeChanged_ReturnsResize()
    {
        var expected = "Resize Frame";
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0f, AT: 0f, AR: 0.4f, AB: 0.5f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    // ── Cut (CompositeCommand via AppCommands) ────────────────────────────────

    [Fact]
    public void CutAnimation_Description_Single_IncludesName()
    {
        var expected = "Cut Animation 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var walk = TestHelpers.MakeChain(ctx.Acls, "Walk");
        TestHelpers.MakeChain(ctx.Acls, "Run");
        var pasted = new AnimationChainSave { Name = "Walk" };

        ctx.AppCommands.PasteChainsCut(new[] { pasted }, new[] { walk });

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void CutFrame_Description_Single_IncludesChainName()
    {
        var expected = "Cut Frame in 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var f0 = chain.Frames[0];
        var pasted = TestHelpers.MakeFrame();

        ctx.AppCommands.PasteFramesCut(chain, new[] { pasted }, insertIndex: 2, sourcesToRemove: new[] { f0 });

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void CutShape_Description_Single_IncludesShapeName()
    {
        var expected = "Cut Rect 'Hitbox'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        frame.ShapesSave!.Shapes.Add(rect);
        var pasted = new AARectSave { Name = "Hitbox" };

        ctx.AppCommands.PasteShapesCut(frame, new[] { pasted }, [], new[] { rect }, frame);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    // ── DeleteChainsCommand / DeleteFramesCommand ─────────────────────────────

    [Fact]
    public void DeleteChainsCommand_Description_SingleChain_IncludesName()
    {
        var expected = "Delete Animation 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = new AnimationChainSave { Name = "Walk" };
        var cmd = new DeleteChainsCommand(
            new[] { chain }, ctx.Acls, ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void DeleteFramesCommand_Description_SingleFrame_IncludesChainName()
    {
        var expected = "Delete Frame in 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var cmd = new DeleteFramesCommand(
            new[] { chain.Frames[0] }, chain, ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    // ── DuplicateChainsCommand / PasteChainsCommand ───────────────────────────

    [Fact]
    public void DuplicateChainsCommand_Description_SingleChain_IncludesName()
    {
        var expected = "Duplicate Animation 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var source = TestHelpers.MakeChain(ctx.Acls, "Walk");
        var copy = new AnimationChainSave { Name = "Walk Copy" };
        var cmd = new DuplicateChainsCommand(
            ctx.Acls,
            new[] { (source, copy) },
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void DuplicateShapesCommand_Description_Single_IncludesShapeName()
    {
        var expected = "Duplicate Rect 'Hitbox'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var copy = new AARectSave { Name = "Hitbox" };
        var cmd = new DuplicateShapesCommand(
            chain.Frames[0], new[] { copy },
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void PasteChainsCommand_Description_SingleChain_IncludesName()
    {
        var expected = "Paste Animation 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = new AnimationChainSave { Name = "Walk" };
        var cmd = new PasteChainsCommand(
            ctx.Acls, new[] { chain },
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void PasteShapesCommand_Description_Single_IncludesShapeName()
    {
        var expected = "Paste Circle 'Hurtbox'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var copy = new CircleSave { Name = "Hurtbox" };
        var cmd = new PasteShapesCommand(
            chain.Frames[0], new[] { copy },
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    // ── FlipCommand ───────────────────────────────────────────────────────────

    [Fact]
    public void FlipCommand_Description_MultipleFrames_IncludesCount()
    {
        var expected = "Flip 3 Frames Horizontal";
        var frames = new[]
        {
            new AnimationFrameSave(),
            new AnimationFrameSave(),
            new AnimationFrameSave(),
        };
        var cmd = new FlipCommand(frames, FlipAxis.Horizontal, commands: null!, events: null!, refresh: () => { });

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void FlipCommand_Description_SingleFrame_OmitsCount()
    {
        var expected = "Flip Vertical";
        var frames = new[] { new AnimationFrameSave() };
        var cmd = new FlipCommand(frames, FlipAxis.Vertical, commands: null!, events: null!, refresh: () => { });

        Assert.Equal(expected, cmd.Description);
    }

    // ── FrameRegionChangedCommand ─────────────────────────────────────────────

    [Fact]
    public void FrameRegionChangedCommand_Description_PositionChangedSizeUnchanged_ReturnsMove()
    {
        var expected = "Move Frame";
        var frame = new AnimationFrameSave();
        // Move: same size (0.5x0.5), just shifted
        var cmd = new FrameRegionChangedCommand(frame,
            bL: 0f,   bT: 0f,   bR: 0.5f, bB: 0.5f,
            aL: 0.1f, aT: 0.1f, aR: 0.6f, aB: 0.6f,
            commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void FrameRegionChangedCommand_Description_SizeChanged_ReturnsResize()
    {
        var expected = "Resize Frame";
        var frame = new AnimationFrameSave();
        // Resize: width changed from 0.5 to 0.4
        var cmd = new FrameRegionChangedCommand(frame,
            bL: 0f,   bT: 0f,   bR: 0.5f, bB: 0.5f,
            aL: 0f,   aT: 0f,   aR: 0.4f, aB: 0.5f,
            commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    // ── InvertFrameOrder / MoveChain / MoveFrame / shape reorder ──────────────

    [Fact]
    public void InvertFrameOrder_Description_IncludesChainName()
    {
        var expected = "Invert Frame Order in 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);

        ctx.AppCommands.InvertFrameOrder(chain);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveChain_Description_IncludesName()
    {
        var expected = "Move Animation 'A' Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        TestHelpers.MakeChain(ctx.Acls, "B");

        ctx.AppCommands.MoveChain(a, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveChainToTop_Description_IncludesName()
    {
        var expected = "Move Animation 'B' to Top";
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");

        ctx.AppCommands.MoveChainToTop(b);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveChainsRelative_Description_MultipleChains_IncludesCount()
    {
        var expected = "Move 2 Animations Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        var b = TestHelpers.MakeChain(ctx.Acls, "B");
        TestHelpers.MakeChain(ctx.Acls, "C");

        ctx.AppCommands.MoveChainsRelative(new[] { a, b }, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveChainsRelative_Description_SingleChain_IncludesName()
    {
        var expected = "Move Animation 'A' Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var a = TestHelpers.MakeChain(ctx.Acls, "A");
        TestHelpers.MakeChain(ctx.Acls, "B");

        ctx.AppCommands.MoveChainsRelative(new[] { a }, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveFrame_Description_IncludesChainName()
    {
        var expected = "Move Frame in 'Walk' Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);

        ctx.AppCommands.MoveFrame(chain.Frames[0], chain, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveFramesCommand_Description_MultipleSameChain_IncludesCount()
    {
        var expected = "Move 3 Frames";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 4);
        var frames = chain.Frames.Take(3).ToArray();
        var cmd = new MoveFramesCommand(
            frames, chain, chain, insertIndex: 3,
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void MoveFramesCommand_Description_SingleSameChain_IncludesChainName()
    {
        var expected = "Move Frame in 'Walk'";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var cmd = new MoveFramesCommand(
            new[] { chain.Frames[0] }, chain, chain, insertIndex: 1,
            ctx.AppCommands, ctx.ApplicationEvents, ctx.SelectedState);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void MoveFramesRelative_Description_MultipleFrames_IncludesCount()
    {
        var expected = "Move 3 Frames Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 5);
        var frames = chain.Frames.Take(3).ToArray();

        ctx.AppCommands.MoveFramesRelative(frames, chain, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveFramesRelative_Description_SingleFrame_IncludesChainName()
    {
        var expected = "Move Frame in 'Walk' Up";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 3);
        var frame = chain.Frames[1];

        ctx.AppCommands.MoveFramesRelative(new[] { frame }, chain, -1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    [Fact]
    public void MoveShape_Description_IncludesShapeName()
    {
        var expected = "Move Rect 'Hitbox' Down";
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 1);
        var frame = chain.Frames[0];
        var rect = new AARectSave { Name = "Hitbox" };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "Other" });

        ctx.AppCommands.MoveShape(rect, frame, +1);

        Assert.Equal(expected, ctx.UndoManager.UndoHistory[^1].Description);
    }

    // ── SetFrameTextureNameCommand ─────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureNameCommand_Description_BothNull_ReturnsSetTexture()
    {
        var expected = "Set Texture";
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: null, newName: null,
            commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void SetFrameTextureNameCommand_Description_NewNameNullOldNameSet_ShowsOldFilename()
    {
        var expected = "Set Texture: old.png";
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: TestPaths.Abs("textures", "old.png"), newName: null,
            commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }

    [Fact]
    public void SetFrameTextureNameCommand_Description_NewNameSet_ShowsFilename()
    {
        var expected = "Set Texture: sprite.png";
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: null, newName: TestPaths.Abs("textures", "sprite.png"),
            commands: null!, events: null!);

        Assert.Equal(expected, cmd.Description);
    }
}
