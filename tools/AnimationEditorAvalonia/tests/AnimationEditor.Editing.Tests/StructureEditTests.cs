using AnimationEditor.Core;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class StructureEditTests : IDisposable
{
    private readonly EditingTestGraph _g = new();
    private readonly AnimationChainListSave _acls = new();
    private readonly AnimationChainSave _walk = new() { Name = "Walk" };
    private readonly AnimationChainSave _run = new() { Name = "Run" };
    private readonly AnimationChainSave _idle = new() { Name = "Idle" };
    private readonly string _folder;

    public StructureEditTests()
    {
        _folder = Path.Combine(_g.Root, "proj");
        Directory.CreateDirectory(_folder);
        foreach (var name in new[] { "a.png", "b.png", "c.png" })
            _walk.Frames.Add(new AnimationFrameSave { TextureName = name, FrameLength = 0.1f });
        _acls.AnimationChains.Add(_walk);
        _acls.AnimationChains.Add(_run);
        _acls.AnimationChains.Add(_idle);
        _g.ProjectManager.AnimationChainListSave = _acls;
        _g.ProjectManager.FileName = Path.Combine(_folder, "hero.achx");
    }

    public void Dispose() => _g.Dispose();

    private string[] Names() => _acls.AnimationChains.Select(c => c.Name).ToArray();
    private string[] Textures() => _walk.Frames.Select(f => f.TextureName).ToArray();

    private string Png(string name, int w = 48, int h = 32)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), w);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), h);
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private EditResult AddFrame(string path, FramePixelRegion? region = null, float? length = null) =>
        StructureEdit.AddFrame(_g.ProjectManager, _g.Commands, _walk, path, region, length, new HeaderTextureProbe());

    // ---- chains ----

    [Fact]
    public void AddChain_AppendsAndUndoes()
    {
        var r = StructureEdit.AddChain(_g.ProjectManager, _g.Commands, "  Jump ");

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Jump"]);
        Names().ShouldBe(["Walk", "Run", "Idle", "Jump"]);
        _g.Undo.Undo();
        Names().ShouldBe(["Walk", "Run", "Idle"]);
    }

    [Fact]
    public void AddChain_DuplicateName_FailsWithDuplicateName()
    {
        var r = StructureEdit.AddChain(_g.ProjectManager, _g.Commands, "Run");

        r.HasError(EditErrorCode.DuplicateName).ShouldBeTrue();
        Names().Length.ShouldBe(3);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void AddChain_Empty_FailsWithEmptyName()
    {
        var r = StructureEdit.AddChain(_g.ProjectManager, _g.Commands, "  ");

        r.HasError(EditErrorCode.EmptyName).ShouldBeTrue();
        Names().Length.ShouldBe(3);
    }

    [Fact]
    public void DeleteChain_RemovesAndUndoes()
    {
        var r = StructureEdit.DeleteChain(_g.ProjectManager, _g.Commands, _run);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Run"]);
        Names().ShouldBe(["Walk", "Idle"]);
        _g.Undo.Undo();
        Names().ShouldBe(["Walk", "Run", "Idle"]);
    }

    [Fact]
    public void DeleteChain_Locked_FailsWithLocked()
    {
        _run.IsLocked = true;

        var r = StructureEdit.DeleteChain(_g.ProjectManager, _g.Commands, _run);

        r.HasError(EditErrorCode.Locked).ShouldBeTrue();
        Names().Length.ShouldBe(3);
    }

    [Fact]
    public void DuplicateChain_DefaultName_IsNumberedAndInsertedAfterSource()
    {
        var r = StructureEdit.DuplicateChain(_g.ProjectManager, _g.Commands, _walk, null);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk2"]);
        Names().ShouldBe(["Walk", "Walk2", "Run", "Idle"]);
        _acls.AnimationChains[1].Frames.Count.ShouldBe(3);
        _acls.AnimationChains[1].Frames.ShouldNotContain(_walk.Frames[0]);
    }

    [Fact]
    public void DuplicateChain_GivenName_IsUsed()
    {
        var r = StructureEdit.DuplicateChain(_g.ProjectManager, _g.Commands, _walk, " Sprint ");

        r.ChangedItems.ShouldBe(["Sprint"]);
        Names().ShouldContain("Sprint");
    }

    [Fact]
    public void DuplicateChain_GivenNameInUse_FailsWithDuplicateName()
    {
        var r = StructureEdit.DuplicateChain(_g.ProjectManager, _g.Commands, _walk, "Idle");

        r.HasError(EditErrorCode.DuplicateName).ShouldBeTrue();
        Names().Length.ShouldBe(3);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void DuplicateChain_EmptyGivenName_FailsWithEmptyName()
    {
        StructureEdit.DuplicateChain(_g.ProjectManager, _g.Commands, _walk, " ").HasError(EditErrorCode.EmptyName).ShouldBeTrue();
    }

    [Fact]
    public void ReorderChain_ToEnd_MovesAndUndoes()
    {
        StructureEdit.ReorderChain(_g.ProjectManager, _g.Commands, _walk, 2).Success.ShouldBeTrue();

        Names().ShouldBe(["Run", "Idle", "Walk"]);
        _g.Undo.Undo();
        Names().ShouldBe(["Walk", "Run", "Idle"]);
    }

    [Fact]
    public void ReorderChain_ToFront_Moves()
    {
        StructureEdit.ReorderChain(_g.ProjectManager, _g.Commands, _idle, 0).Success.ShouldBeTrue();

        Names().ShouldBe(["Idle", "Walk", "Run"]);
    }

    [Fact]
    public void ReorderChain_SameIndex_SucceedsWithNoUndoEntry()
    {
        StructureEdit.ReorderChain(_g.ProjectManager, _g.Commands, _run, 1).Success.ShouldBeTrue();

        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void ReorderChain_OutOfRange_FailsWithIndexOutOfRange(int index)
    {
        var r = StructureEdit.ReorderChain(_g.ProjectManager, _g.Commands, _run, index);

        r.HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
        Names().ShouldBe(["Walk", "Run", "Idle"]);
    }

    // ---- frames ----

    [Fact]
    public void AddFrame_WholeTextureAndDefaultLength()
    {
        var r = AddFrame(Png("d.png"));

        r.Success.ShouldBeTrue(string.Join(";", r.Errors));
        r.ChangedItems.ShouldBe(["Walk/3"]);
        var f = _walk.Frames[3];
        f.TextureName.ShouldBe("d.png");
        (f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate, f.BottomCoordinate).ShouldBe((0f, 0f, 1f, 1f));
        f.FrameLength.ShouldBe(FrameLengthUnit.Of(_acls).DefaultFrameLength);
        _g.Undo.Undo();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void AddFrame_WithPixelRegionAndLength()
    {
        var r = AddFrame(Png("d.png", 48, 32), new FramePixelRegion(16, 8, 16, 16), 0.25f);

        r.Success.ShouldBeTrue(string.Join(";", r.Errors));
        var f = _walk.Frames[3];
        f.LeftCoordinate.ShouldBe(16f / 48, 0.0001f);
        f.RightCoordinate.ShouldBe(32f / 48, 0.0001f);
        f.TopCoordinate.ShouldBe(8f / 32, 0.0001f);
        f.BottomCoordinate.ShouldBe(24f / 32, 0.0001f);
        f.FrameLength.ShouldBe(0.25f);
    }

    [Fact]
    public void AddFrame_MissingTexture_FailsWithTextureNotFound()
    {
        var r = AddFrame(Path.Combine(_folder, "nope.png"));

        r.HasError(EditErrorCode.TextureNotFound).ShouldBeTrue();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void AddFrame_NotAnImage_FailsWithTextureUndecodable()
    {
        var path = Path.Combine(_folder, "fake.png");
        File.WriteAllText(path, "text");

        var r = AddFrame(path);

        r.HasError(EditErrorCode.TextureUndecodable).ShouldBeTrue();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(-1, 0, 4, 4)]
    [InlineData(0, 0, 0, 4)]
    [InlineData(40, 0, 16, 4)]
    [InlineData(0, 30, 4, 4)]
    public void AddFrame_RegionOutsideTexture_FailsAndAddsNothing(int x, int y, int w, int h)
    {
        var r = AddFrame(Png("d.png", 48, 32), new FramePixelRegion(x, y, w, h));

        r.Success.ShouldBeFalse();
        _walk.Frames.Count.ShouldBe(3);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void AddFrame_RegionOnNonPng_Fails()
    {
        var path = Path.Combine(_folder, "d.bmp");
        File.WriteAllBytes(path, [0x42, 0x4D, 0, 0, 0, 0, 0, 0, 0, 0]);

        var r = AddFrame(path, new FramePixelRegion(0, 0, 4, 4));

        r.Success.ShouldBeFalse();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void AddFrame_NonPositiveLength_FailsAndAddsNothing(float length)
    {
        var r = AddFrame(Png("d.png"), null, length);

        r.Success.ShouldBeFalse();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void AddFrame_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        AddFrame(Png("d.png")).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void DeleteFrame_RemovesAndUndoes()
    {
        var r = StructureEdit.DeleteFrame(_g.Commands, _walk, _walk.Frames[1]);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/1"]);
        Textures().ShouldBe(["a.png", "c.png"]);
        _g.Undo.Undo();
        Textures().ShouldBe(["a.png", "b.png", "c.png"]);
    }

    [Fact]
    public void DeleteFrame_Locked_FailsWithLocked()
    {
        _walk.IsLocked = true;

        StructureEdit.DeleteFrame(_g.Commands, _walk, _walk.Frames[1]).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void DuplicateFrame_InsertsCopyAfterSource()
    {
        var source = _walk.Frames[1];
        source.FrameLength = 0.4f;

        var r = StructureEdit.DuplicateFrame(_g.Commands, _walk, source);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/2"]);
        Textures().ShouldBe(["a.png", "b.png", "b.png", "c.png"]);
        _walk.Frames[2].ShouldNotBeSameAs(source);
        _walk.Frames[2].FrameLength.ShouldBe(0.4f);
    }

    [Fact]
    public void DuplicateFrame_Locked_FailsWithLocked()
    {
        _walk.IsLocked = true;

        StructureEdit.DuplicateFrame(_g.Commands, _walk, _walk.Frames[0]).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _walk.Frames.Count.ShouldBe(3);
    }

    [Fact]
    public void ReorderFrame_MovesToFinalIndexAndUndoes()
    {
        var r = StructureEdit.ReorderFrame(_g.Commands, _walk, _walk.Frames[0], 2);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk/2"]);
        Textures().ShouldBe(["b.png", "c.png", "a.png"]);
        _g.Undo.Undo();
        Textures().ShouldBe(["a.png", "b.png", "c.png"]);
    }

    [Fact]
    public void ReorderFrame_SameIndex_SucceedsWithNoUndoEntry()
    {
        StructureEdit.ReorderFrame(_g.Commands, _walk, _walk.Frames[1], 1).Success.ShouldBeTrue();

        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void ReorderFrame_OutOfRange_FailsWithIndexOutOfRange(int index)
    {
        StructureEdit.ReorderFrame(_g.Commands, _walk, _walk.Frames[1], index).HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
        Textures().ShouldBe(["a.png", "b.png", "c.png"]);
    }
}
