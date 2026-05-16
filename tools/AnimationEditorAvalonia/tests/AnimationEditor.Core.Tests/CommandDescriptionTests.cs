using AnimationEditor.Core.CommandsAndState.Commands;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class CommandDescriptionTests
{
    // ── FrameRegionChangedCommand ─────────────────────────────────────────────

    [Fact]
    public void FrameRegionChangedCommand_Description_PositionChangedSizeUnchanged_ReturnsMove()
    {
        var frame = new AnimationFrameSave();
        // Move: same size (0.5x0.5), just shifted
        var cmd = new FrameRegionChangedCommand(frame,
            bL: 0f,   bT: 0f,   bR: 0.5f, bB: 0.5f,
            aL: 0.1f, aT: 0.1f, aR: 0.6f, aB: 0.6f,
            commands: null!, events: null!);

        Assert.Equal("Move Frame", cmd.Description);
    }

    [Fact]
    public void FrameRegionChangedCommand_Description_SizeChanged_ReturnsResize()
    {
        var frame = new AnimationFrameSave();
        // Resize: width changed from 0.5 to 0.4
        var cmd = new FrameRegionChangedCommand(frame,
            bL: 0f,   bT: 0f,   bR: 0.5f, bB: 0.5f,
            aL: 0f,   aT: 0f,   aR: 0.4f, aB: 0.5f,
            commands: null!, events: null!);

        Assert.Equal("Resize Frame", cmd.Description);
    }

    // ── BulkFrameRegionChangedCommand ─────────────────────────────────────────

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_SingleFramePositionOnly_ReturnsMove()
    {
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0.1f, AT: 0.1f, AR: 0.6f, AB: 0.6f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal("Move Frame", cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_SingleFrameSizeChanged_ReturnsResize()
    {
        var snapshots = new[]
        {
            new BulkFrameRegionChangedCommand.FrameSnapshot(
                new AnimationFrameSave(),
                BL: 0f, BT: 0f, BR: 0.5f, BB: 0.5f,
                AL: 0f, AT: 0f, AR: 0.4f, AB: 0.5f)
        };
        var cmd = new BulkFrameRegionChangedCommand(snapshots, commands: null!, events: null!);

        Assert.Equal("Resize Frame", cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_MultiFrameSameDelta_ReturnsDragChain()
    {
        // Both frames move by (+0.1, +0.1) with same size — chain drag
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

        Assert.Equal("Drag Chain (2 frames)", cmd.Description);
    }

    [Fact]
    public void BulkFrameRegionChangedCommand_Description_MultiFrameDifferentDeltas_ReturnsEditRegions()
    {
        // Three frames with different translation deltas — handle drag
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

        Assert.Equal("Edit 3 Frame Regions", cmd.Description);
    }

    // ── SetFrameTextureNameCommand ─────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureNameCommand_Description_NewNameSet_ShowsFilename()
    {
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: null, newName: @"C:\textures\sprite.png",
            commands: null!, events: null!);

        Assert.Equal("Set Texture: sprite.png", cmd.Description);
    }

    [Fact]
    public void SetFrameTextureNameCommand_Description_NewNameNullOldNameSet_ShowsOldFilename()
    {
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: @"C:\textures\old.png", newName: null,
            commands: null!, events: null!);

        Assert.Equal("Set Texture: old.png", cmd.Description);
    }

    [Fact]
    public void SetFrameTextureNameCommand_Description_BothNull_ReturnsSetTexture()
    {
        var frame = new AnimationFrameSave();
        var cmd = new SetFrameTextureNameCommand(frame,
            oldName: null, newName: null,
            commands: null!, events: null!);

        Assert.Equal("Set Texture", cmd.Description);
    }
}
