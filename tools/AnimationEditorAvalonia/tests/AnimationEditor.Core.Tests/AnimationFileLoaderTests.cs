using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AnimationFileLoaderTests
{
    // Minimal 24-byte fake PNG (valid signature + IHDR width/height fields).
    private static byte[] MakeFakePng(int width, int height)
    {
        var b = new byte[24];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47;
        b[4] = 0x0D; b[5] = 0x0A; b[6] = 0x1A; b[7] = 0x0A;
        b[8] = 0; b[9] = 0; b[10] = 0; b[11] = 13;
        b[12] = 0x49; b[13] = 0x48; b[14] = 0x44; b[15] = 0x52;
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16);
        b[18] = (byte)(width >> 8);  b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16);
        b[22] = (byte)(height >> 8);  b[23] = (byte)height;
        return b;
    }

    [Fact]
    public void Load_PixelFile_ReturnsUvModelAndRemembersPixelOnDisk()
    {
        using var dir = new TestHelpers.TempDir();
        File.WriteAllBytes(Path.Combine(dir.Path, "hero.png"), MakeFakePng(64, 32));
        var onDisk = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "hero.png", LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 8, BottomCoordinate = 16,
        });
        onDisk.AnimationChains.Add(chain);
        string path = Path.Combine(dir.Path, "hero.achj");
        onDisk.SaveJson(path);

        var loaded = AnimationFileLoader.Load(path);

        loaded.OnDiskCoordinateType.ShouldBe(TextureCoordinateType.Pixel);
        loaded.List.CoordinateType.ShouldBe(TextureCoordinateType.UV);
        var frame = loaded.List.AnimationChains[0].Frames[0];
        (frame.LeftCoordinate, frame.RightCoordinate, frame.TopCoordinate, frame.BottomCoordinate)
            .ShouldBe((0.25f, 0.5f, 0.25f, 0.5f));
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        using var dir = new TestHelpers.TempDir();

        Should.Throw<FileNotFoundException>(() => AnimationFileLoader.Load(Path.Combine(dir.Path, "gone.achx")));
    }

    [Fact]
    public void Load_ConflictMarkers_ThrowsInvalidData()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Path.Combine(dir.Path, "merged.achx");
        File.WriteAllText(path, "<AnimationChainArraySave>\n<<<<<<< HEAD\n=======\n>>>>>>> branch\n</AnimationChainArraySave>");

        Should.Throw<InvalidDataException>(() => AnimationFileLoader.Load(path));
    }
}
