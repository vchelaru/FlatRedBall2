using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class PropertyEditTests : IDisposable
{
    private readonly EditingTestGraph _g = new();
    private readonly AnimationChainListSave _acls = new() { TimeMeasurementUnit = TimeMeasurementUnit.Second };
    private readonly AnimationChainSave _walk = new() { Name = "Walk" };
    private readonly AnimationChainSave _run = new() { Name = "Run" };
    private readonly string _folder;

    public PropertyEditTests()
    {
        _folder = Path.Combine(_g.Root, "proj");
        Directory.CreateDirectory(_folder);
        for (int i = 0; i < 3; i++)
            _walk.Frames.Add(new AnimationFrameSave
            {
                TextureName = "sheet.png", FrameLength = 0.1f * (i + 1),
                LeftCoordinate = i / 3f, RightCoordinate = (i + 1) / 3f, TopCoordinate = 0, BottomCoordinate = 1,
            });
        _run.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.5f });
        _acls.AnimationChains.Add(_walk);
        _acls.AnimationChains.Add(_run);
        _g.ProjectManager.AnimationChainListSave = _acls;
        _g.ProjectManager.FileName = Path.Combine(_folder, "hero.achx");
        WritePng("sheet.png", 48, 32);
    }

    public void Dispose() => _g.Dispose();

    private void WritePng(string name, int w, int h)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), w);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), h);
        File.WriteAllBytes(Path.Combine(_folder, name), bytes);
    }

    private AnimationFrameSave F(int i) => _walk.Frames[i];

    // ---- chain flags ----

    [Fact]
    public void SetChainLoop_ChangesAndUndoesInOneStep()
    {
        var r = PropertyEdit.SetChainLoop(_g.Commands, _walk, false);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBe(["Walk"]);
        _walk.Loop.ShouldBeFalse();
        _g.Undo.Undo();
        _walk.Loop.ShouldBeTrue();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetChainPingPong_ChangesAndUndoes()
    {
        var r = PropertyEdit.SetChainPingPong(_g.Commands, _walk, true);

        r.ChangedItems.ShouldBe(["Walk"]);
        _walk.PingPong.ShouldBeTrue();
        _g.Undo.Undo();
        _walk.PingPong.ShouldBeFalse();
    }

    [Fact]
    public void SetChainReverse_ChangesAndUndoes()
    {
        var r = PropertyEdit.SetChainReverse(_g.Commands, _walk, true);

        r.ChangedItems.ShouldBe(["Walk"]);
        _walk.Reverse.ShouldBeTrue();
        _g.Undo.Undo();
        _walk.Reverse.ShouldBeFalse();
    }

    [Fact]
    public void SetChainLocked_ChangesAndUndoes_AndWorksOnALockedChain()
    {
        PropertyEdit.SetChainLocked(_g.Commands, _walk, true).ChangedItems.ShouldBe(["Walk"]);
        _walk.IsLocked.ShouldBeTrue();

        var unlock = PropertyEdit.SetChainLocked(_g.Commands, _walk, false);

        unlock.Success.ShouldBeTrue();
        _walk.IsLocked.ShouldBeFalse();
    }

    [Fact]
    public void ChainFlags_AlreadyAtValue_ChangeNothing()
    {
        var r = PropertyEdit.SetChainLoop(_g.Commands, _walk, true);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void ChainFlags_LockedChain_FailWithLocked()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetChainLoop(_g.Commands, _walk, false).HasError(EditErrorCode.Locked).ShouldBeTrue();
        PropertyEdit.SetChainPingPong(_g.Commands, _walk, true).HasError(EditErrorCode.Locked).ShouldBeTrue();
        PropertyEdit.SetChainReverse(_g.Commands, _walk, true).HasError(EditErrorCode.Locked).ShouldBeTrue();
        _walk.Loop.ShouldBeTrue();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    // ---- frame length ----

    [Fact]
    public void SetFrameLength_SetsInFileUnitAndUndoes()
    {
        var r = PropertyEdit.SetFrameLength(_g.ProjectManager, _g.Commands, _walk, F(1), 0.35f);

        r.ChangedItems.ShouldBe(["Walk/1"]);
        F(1).FrameLength.ShouldBe(0.35f);
        _g.Undo.Undo();
        F(1).FrameLength.ShouldBe(0.2f, 0.0001f);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameLength_MillisecondFile_UsesMilliseconds()
    {
        _acls.TimeMeasurementUnit = TimeMeasurementUnit.Millisecond;

        PropertyEdit.SetFrameLength(_g.ProjectManager, _g.Commands, _walk, F(0), 250f).Success.ShouldBeTrue();

        F(0).FrameLength.ShouldBe(250f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(0.0005f)]
    [InlineData(61f)]
    [InlineData(float.NaN)]
    public void SetFrameLength_OutOfRangeForSeconds_FailsWithRangeInMessage(float length)
    {
        var r = PropertyEdit.SetFrameLength(_g.ProjectManager, _g.Commands, _walk, F(0), length);

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("0.001");
        F(0).FrameLength.ShouldBe(0.1f);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameLength_SameValue_ChangesNothing()
    {
        var r = PropertyEdit.SetFrameLength(_g.ProjectManager, _g.Commands, _walk, F(0), 0.1f);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameLength_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetFrameLength(_g.ProjectManager, _g.Commands, _walk, F(0), 0.5f).HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(0).FrameLength.ShouldBe(0.1f);
    }

    // ---- region ----

    [Fact]
    public void SetFrameRegion_SetsPixelRectangleAndUndoes()
    {
        var r = PropertyEdit.SetFrameRegion(_g.ProjectManager, _g.Commands, _walk, F(0), new FramePixelRegion(16, 8, 16, 16));

        r.Success.ShouldBeTrue(string.Join(";", r.Errors));
        r.ChangedItems.ShouldBe(["Walk/0"]);
        F(0).LeftCoordinate.ShouldBe(16f / 48, 0.0001f);
        F(0).RightCoordinate.ShouldBe(32f / 48, 0.0001f);
        F(0).TopCoordinate.ShouldBe(8f / 32, 0.0001f);
        F(0).BottomCoordinate.ShouldBe(24f / 32, 0.0001f);
        _g.Undo.Undo();
        F(0).LeftCoordinate.ShouldBe(0f);
        F(0).RightCoordinate.ShouldBe(1f / 3, 0.0001f);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1, 0, 4, 4)]
    [InlineData(0, 0, 0, 4)]
    [InlineData(0, 0, 4, -2)]
    [InlineData(40, 0, 16, 4)]
    [InlineData(0, 30, 4, 4)]
    public void SetFrameRegion_OutsideTexture_FailsWithSizeInMessage(int x, int y, int w, int h)
    {
        var r = PropertyEdit.SetFrameRegion(_g.ProjectManager, _g.Commands, _walk, F(0), new FramePixelRegion(x, y, w, h));

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("48x32");
        F(0).LeftCoordinate.ShouldBe(0f);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameRegion_MissingTexture_FailsWithTextureNotFound()
    {
        File.Delete(Path.Combine(_folder, "sheet.png"));

        var r = PropertyEdit.SetFrameRegion(_g.ProjectManager, _g.Commands, _walk, F(0), new FramePixelRegion(0, 0, 4, 4));

        r.HasError(EditErrorCode.TextureNotFound).ShouldBeTrue();
    }

    [Fact]
    public void SetFrameRegion_NonPngTexture_Fails()
    {
        File.WriteAllBytes(Path.Combine(_folder, "sheet.bmp"), [0x42, 0x4D, 0, 0, 0, 0, 0, 0, 0, 0]);
        F(0).TextureName = "sheet.bmp";

        var r = PropertyEdit.SetFrameRegion(_g.ProjectManager, _g.Commands, _walk, F(0), new FramePixelRegion(0, 0, 4, 4));

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("PNG");
    }

    [Fact]
    public void SetFrameRegion_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        var r = PropertyEdit.SetFrameRegion(_g.ProjectManager, _g.Commands, _walk, F(0), new FramePixelRegion(0, 0, 4, 4));

        r.HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    // ---- offset ----

    [Fact]
    public void SetFrameOffset_SetsBothAndUndoes()
    {
        var r = PropertyEdit.SetFrameOffset(_g.Commands, _walk, F(2), 3f, -4.5f);

        r.ChangedItems.ShouldBe(["Walk/2"]);
        (F(2).RelativeX, F(2).RelativeY).ShouldBe((3f, -4.5f));
        _g.Undo.Undo();
        (F(2).RelativeX, F(2).RelativeY).ShouldBe((0f, 0f));
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameOffset_OnlyX_LeavesY()
    {
        F(2).RelativeY = 7f;

        PropertyEdit.SetFrameOffset(_g.Commands, _walk, F(2), 3f, null).Success.ShouldBeTrue();

        (F(2).RelativeX, F(2).RelativeY).ShouldBe((3f, 7f));
    }

    [Fact]
    public void SetFrameOffset_NeitherGiven_Fails()
    {
        PropertyEdit.SetFrameOffset(_g.Commands, _walk, F(2), null, null).Success.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameOffset_NonFinite_Fails()
    {
        PropertyEdit.SetFrameOffset(_g.Commands, _walk, F(2), float.PositiveInfinity, 0f).Success.ShouldBeFalse();
        F(2).RelativeX.ShouldBe(0f);
    }

    [Fact]
    public void SetFrameOffset_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetFrameOffset(_g.Commands, _walk, F(2), 1f, 1f).HasError(EditErrorCode.Locked).ShouldBeTrue();
    }

    // ---- color ----

    [Fact]
    public void SetFrameColor_SetsAllChannelsAndOperationInOneUndoStep()
    {
        var r = PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1),
            new FrameColorEdit(Red: 255, Green: 128, Blue: 0, Alpha: 200, Operation: ColorOperation.Add));

        r.ChangedItems.ShouldBe(["Walk/1"]);
        (F(1).Red, F(1).Green, F(1).Blue, F(1).Alpha, F(1).ColorOperation).ShouldBe((255, 128, 0, 200, ColorOperation.Add));
        _g.Undo.Undo();
        (F(1).Red, F(1).Green, F(1).Blue, F(1).Alpha, F(1).ColorOperation).ShouldBe((null, null, null, null, null));
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameColor_OnlyAlpha_LeavesTheRest()
    {
        F(1).Red = 10;

        PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), new FrameColorEdit(Alpha: 64)).Success.ShouldBeTrue();

        (F(1).Red, F(1).Alpha).ShouldBe((10, 64));
    }

    [Theory]
    [InlineData(256)]
    [InlineData(-256)]
    public void SetFrameColor_ChannelOutOfRange_Fails(int red)
    {
        var r = PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), new FrameColorEdit(Red: red));

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("-255");
        F(1).Red.ShouldBeNull();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameColor_AlphaOutOfRange_Fails()
    {
        var r = PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), new FrameColorEdit(Alpha: 256));

        r.Success.ShouldBeFalse();
        r.Errors[0].ShouldContain("0 to 255");
    }

    [Fact]
    public void SetFrameColor_NothingGiven_Fails()
    {
        PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), default).Success.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameColor_SameValues_ChangeNothing()
    {
        F(1).Alpha = 50;

        var r = PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), new FrameColorEdit(Alpha: 50));

        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameColor_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetFrameColor(_g.Commands, _walk, F(1), new FrameColorEdit(Alpha: 1)).HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(1).Alpha.ShouldBeNull();
    }

    // ---- flip ----

    [Fact]
    public void SetFrameFlip_SetsAbsoluteFlagsInOneUndoStep()
    {
        var r = PropertyEdit.SetFrameFlip(_g.Commands, _walk, F(0), true, null, true);

        r.ChangedItems.ShouldBe(["Walk/0"]);
        (F(0).FlipHorizontal, F(0).FlipVertical, F(0).FlipDiagonal).ShouldBe((true, false, true));
        _g.Undo.Undo();
        (F(0).FlipHorizontal, F(0).FlipVertical, F(0).FlipDiagonal).ShouldBe((false, false, false));
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameFlip_AlreadyAtValue_ChangesNothing()
    {
        var r = PropertyEdit.SetFrameFlip(_g.Commands, _walk, F(0), false, false, false);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameFlip_NoFlagGiven_Fails()
    {
        PropertyEdit.SetFrameFlip(_g.Commands, _walk, F(0), null, null, null).Success.ShouldBeFalse();
    }

    [Fact]
    public void SetFrameFlip_LockedChain_FailsWithLocked()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetFrameFlip(_g.Commands, _walk, F(0), true, null, null).HasError(EditErrorCode.Locked).ShouldBeTrue();
        F(0).FlipHorizontal.ShouldBeFalse();
    }

    // ---- unit ----

    [Fact]
    public void SetTimeUnit_SecondToMillisecond_ConvertsEveryLengthAndKeepsPlaybackTime()
    {
        var before = _acls.AnimationChains.SelectMany(c => c.Frames).Select(f => FrameLengthUnit.Of(_acls).ToSeconds(f.FrameLength)).ToArray();

        var r = PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Millisecond);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldNotBeEmpty();
        _acls.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
        var unit = FrameLengthUnit.Of(_acls);
        var after = _acls.AnimationChains.SelectMany(c => c.Frames).Select(f => unit.ToSeconds(f.FrameLength)).ToArray();
        for (int i = 0; i < before.Length; i++) after[i].ShouldBe(before[i], 0.0005);
        F(0).FrameLength.ShouldBe(100f, 0.01f);
    }

    [Fact]
    public void SetTimeUnit_UndoRestoresUnitAndLengthsInOneStep()
    {
        PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Millisecond);

        _g.Undo.Undo();

        _acls.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Second);
        F(0).FrameLength.ShouldBe(0.1f, 0.0001f);
        _run.Frames[0].FrameLength.ShouldBe(0.5f);
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetTimeUnit_MillisecondToSecond_Converts()
    {
        _acls.TimeMeasurementUnit = TimeMeasurementUnit.Millisecond;
        F(0).FrameLength = 250f;

        PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Second).Success.ShouldBeTrue();

        _acls.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Second);
        F(0).FrameLength.ShouldBe(0.25f, 0.0001f);
    }

    [Fact]
    public void SetTimeUnit_SameUnit_ChangesNothing()
    {
        var r = PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Second);

        r.Success.ShouldBeTrue();
        r.ChangedItems.ShouldBeEmpty();
        _g.Undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SetTimeUnit_UndefinedFile_ToSecond_RecordsUnitWithoutChangingLengths()
    {
        _acls.TimeMeasurementUnit = TimeMeasurementUnit.Undefined;

        var r = PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Second);

        r.ChangedItems.ShouldNotBeEmpty();
        _acls.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Second);
        F(0).FrameLength.ShouldBe(0.1f);
    }

    [Fact]
    public void SetTimeUnit_Undefined_Fails()
    {
        PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Undefined).Success.ShouldBeFalse();
    }

    [Fact]
    public void SetTimeUnit_LockedChain_StillConvertsItsLengths()
    {
        _walk.IsLocked = true;

        PropertyEdit.SetTimeUnit(_g.ProjectManager, _g.Commands, TimeMeasurementUnit.Millisecond).Success.ShouldBeTrue();

        F(0).FrameLength.ShouldBe(100f, 0.01f);
    }
}
