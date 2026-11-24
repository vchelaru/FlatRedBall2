using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>The open document keeps frame regions in pixels (#1952), so no texture change on the way can rescale one.</summary>
public class PixelDocumentTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    private string PathOf(string name) => Path.Combine(_dir.Path, name);

    // The size the editor reads is in the PNG header; nothing here decodes pixels.
    private void WritePng(string name, int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), bytes);
    }

    private static AnimationFrameSave Frame(string texture, float left, float top, float right, float bottom) => new()
    {
        TextureName = texture, FrameLength = 0.1f,
        LeftCoordinate = left, TopCoordinate = top, RightCoordinate = right, BottomCoordinate = bottom,
    };

    private string WriteFile(string name, params AnimationFrameSave[] frames)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, TimeMeasurementUnit = TimeMeasurementUnit.Second };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.AddRange(frames);
        list.AnimationChains.Add(chain);
        if (name.EndsWith(".achj")) list.SaveJson(PathOf(name)); else list.Save(PathOf(name));
        return PathOf(name);
    }

    private AnimationFrameSave[] Open(string path)
    {
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(path));
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Frames.ToArray();
    }

    private AnimationFrameSave[] SaveAndRead(string path)
    {
        _ctx.ProjectManager.SaveAnimationChainList(path);
        return (path.EndsWith(".achj") ? AnimationChainListSave.FromJsonFile(path) : AnimationChainListSave.FromFile(path))
            .AnimationChains[0].Frames.ToArray();
    }

    private static (float, float, float, float) Region(AnimationFrameSave f) =>
        (f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate, f.BottomCoordinate);

    // #1934
    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public void SetFrameTextureName_TextureMissingAtLoad_KeepsThePixelRegion(string file)
    {
        WritePng("present.png", 96, 96);
        var path = WriteFile(file, Frame("sheet.png", 0, 0, 16, 32), Frame("present.png", 32, 0, 48, 32));
        var frames = Open(path);
        WritePng("art/sheet.png", 48, 64);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "art/sheet.png");

        var saved = SaveAndRead(path);
        Region(saved[0]).ShouldBe((0f, 0f, 16f, 32f));
        Region(saved[1]).ShouldBe((32f, 0f, 48f, 32f));
    }

    [Fact]
    public void SetFrameTextureName_UndoneAndRedone_KeepsThePixelRegion()
    {
        var path = WriteFile("hero.achx", Frame("sheet.png", 0, 0, 16, 32));
        var frames = Open(path);
        WritePng("art/sheet.png", 48, 64);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "art/sheet.png");
        _ctx.UndoManager.Undo();
        Region(frames[0]).ShouldBe((0f, 0f, 16f, 32f));
        _ctx.UndoManager.Redo();

        Region(SaveAndRead(path)[0]).ShouldBe((0f, 0f, 16f, 32f));
    }

    // #1947
    [Fact]
    public void SetFrameTextureName_TextureOfAnotherSize_KeepsThePixelRegion()
    {
        WritePng("sheet.png", 48, 32);
        WritePng("tall.png", 48, 64);
        var path = WriteFile("hero.achx", Frame("sheet.png", 0, 0, 16, 32));
        var frames = Open(path);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "tall.png");

        Region(SaveAndRead(path)[0]).ShouldBe((0f, 0f, 16f, 32f));
    }

    [Fact]
    public void SaveAnimationChainList_PngChangedSizeOnDiskSinceLoad_KeepsThePixelRegion()
    {
        WritePng("sheet.png", 48, 32);
        var path = WriteFile("hero.achx", Frame("sheet.png", 0, 0, 16, 32));
        Open(path);
        WritePng("sheet.png", 96, 128);

        Region(SaveAndRead(path)[0]).ShouldBe((0f, 0f, 16f, 32f));
    }

    // A 37x53 sheet: a trip through fractions of it is not exact in floats. The textureless frame keeps the
    // unset region, and the missing texture's frame is written back as it was.
    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public void SaveAnimationChainList_UneditedPixelFile_IsByteIdentical(string file)
    {
        WritePng("sheet.png", 37, 53);
        var path = WriteFile(file,
            Frame("sheet.png", 7, 11, 30, 49), Frame("sheet.png", 0.5f, 1.25f, 13.7f, 41.1f),
            Frame("missing.png", 3, 5, 16, 32), Frame("", 0, 0, 1, 1), Frame("", 4, 4, 9, 9));
        var before = File.ReadAllBytes(path);

        Open(path);
        _ctx.ProjectManager.SaveAnimationChainList(path);

        File.ReadAllBytes(path).ShouldBe(before);
    }

    // ── A region nobody has set (0,0,1,1) shows the whole texture once its size is known ──

    [Fact]
    public void AddFrame_WithATexture_ShowsTheWholeTexture()
    {
        WritePng("sheet.png", 48, 32);
        var path = WriteFile("hero.achx", Frame("sheet.png", 0, 0, 16, 32));
        Open(path);
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];

        _ctx.AppCommands.AddFrame(chain, "sheet.png");

        Region(chain.Frames[^1]).ShouldBe((0f, 0f, 48f, 32f));
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing.png")]
    public void SetFrameTextureName_UnsetRegion_ShowsTheWholeTexture_AndUndoRestoresTheUnsetRegion(string texture)
    {
        WritePng("sheet.png", 48, 32);
        var path = WriteFile("hero.achx", Frame(texture, 0, 0, 1, 1));
        var frames = Open(path);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "sheet.png");
        Region(frames[0]).ShouldBe((0f, 0f, 48f, 32f));
        _ctx.UndoManager.Undo();
        Region(frames[0]).ShouldBe((0f, 0f, 1f, 1f));
        _ctx.UndoManager.Redo();

        Region(SaveAndRead(path)[0]).ShouldBe((0f, 0f, 48f, 32f));
    }

    [Fact]
    public void SetFrameTextureName_UnreadableTexture_KeepsTheUnsetRegion()
    {
        var path = WriteFile("hero.achx", Frame("", 0, 0, 1, 1));
        var frames = Open(path);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "photo.jpg");

        Region(SaveAndRead(path)[0]).ShouldBe((0f, 0f, 1f, 1f));
    }

    [Fact]
    public void SetFrameTextureName_ClearedAndSetAgain_KeepsThePixelRegion()
    {
        WritePng("sheet.png", 48, 32);
        WritePng("tall.png", 48, 64);
        var path = WriteFile("hero.achx", Frame("sheet.png", 16, 8, 32, 24));
        var frames = Open(path);

        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, string.Empty);
        Region(SaveAndRead(path)[0]).ShouldBe((16f, 8f, 32f, 24f));
        frames = Open(path);
        _ctx.AppCommands.SetFrameTextureName(new[] { frames[0] }, "tall.png");

        Region(SaveAndRead(path)[0]).ShouldBe((16f, 8f, 32f, 24f));
    }

    // ── UV files convert at the boundary ──

    private string WriteUvFile(string texture)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(Frame(texture, 0.25f, 0f, 0.5f, 0.75f));
        list.AnimationChains.Add(chain);
        list.Save(PathOf("old.achx"));
        return PathOf("old.achx");
    }

    [Fact]
    public void LoadAnimationChain_UvFile_HoldsPixels_AndSavesTheSameUv()
    {
        WritePng("sheet.png", 64, 32);
        var path = WriteUvFile("sheet.png");
        var before = File.ReadAllBytes(path);

        var frames = Open(path);

        _ctx.ProjectManager.AnimationChainListSave!.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);
        Region(frames[0]).ShouldBe((16f, 0f, 32f, 24f));
        _ctx.ProjectManager.SaveAnimationChainList(path);
        File.ReadAllBytes(path).ShouldBe(before);
        Region(frames[0]).ShouldBe((16f, 0f, 32f, 24f));
    }

    [Fact]
    public void LoadAnimationChain_UvFileWithAMissingTexture_IsRefused_AndTheOpenDocumentStays()
    {
        var open = Open(WriteFile("hero.achx", Frame("sheet.png", 0, 0, 16, 32)));
        var path = WriteUvFile("gone.png");

        Should.Throw<InvalidOperationException>(() => Open(path)).Message.ShouldContain("gone.png");

        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Frames[0].ShouldBeSameAs(open[0]);
    }

    [Fact]
    public void RecoveryFile_LeftInUvByAnOlderEditor_IsReadAsPixels()
    {
        WritePng("sheet.png", 64, 32);
        _ctx.IoManager.RecoveryFilePath = WriteUvFile(PathOf("sheet.png"));

        var recovered = _ctx.IoManager.TryReadRecoveryFile()!;

        Region(recovered.AnimationChains[0].Frames[0]).ShouldBe((16f, 0f, 32f, 24f));
    }

    // ── Clipboard ──

    [Fact]
    public void Clipboard_PixelText_KeepsTheRegion_EvenWhenTheTextureIsMissing()
    {
        var copied = new List<AnimationFrameSave> { Frame("sheet.png", 3, 5, 16, 32) };

        var text = ClipboardPayload.Serialize(copied, FrameLengthUnit.Seconds);

        ClipboardPayload.TryDeserialize(text, FrameLengthUnit.Seconds, _ => null, out _, out var pasted, out _).ShouldBeTrue();
        Region(pasted!.Single()).ShouldBe((3f, 5f, 16f, 32f));
    }

    // Text an editor before #1952 copied: the list's own UV header, or none at all.
    [Theory]
    [InlineData("<CoordinateType>UV</CoordinateType>")]
    [InlineData("")]
    public void Clipboard_OldUvText_ConvertsToPixels_AndIsUnsetWhenTheTextureIsMissing(string header)
    {
        var text = $"List<AnimationChainSave>:<AnimationChainArraySave>{header}<AnimationChain><Name>Walk</Name>" +
            "<Frame><TextureName>sheet.png</TextureName><LeftCoordinate>0.25</LeftCoordinate><RightCoordinate>0.5</RightCoordinate>" +
            "<TopCoordinate>0</TopCoordinate><BottomCoordinate>0.75</BottomCoordinate></Frame>" +
            "<Frame><TextureName>gone.png</TextureName><LeftCoordinate>0.25</LeftCoordinate><RightCoordinate>0.5</RightCoordinate>" +
            "<TopCoordinate>0</TopCoordinate><BottomCoordinate>0.75</BottomCoordinate></Frame></AnimationChain></AnimationChainArraySave>";

        ClipboardPayload.TryDeserialize(text, FrameLengthUnit.Seconds, name => name == "sheet.png" ? (64, 32) : null,
            out var chains, out _, out _).ShouldBeTrue();

        Region(chains!.Single().Frames[0]).ShouldBe((16f, 0f, 32f, 24f));
        Region(chains!.Single().Frames[1]).ShouldBe((0f, 0f, 1f, 1f));
    }
}
