using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1624: the filmstrip caches each chain's laid-out cells, whose widths come from the source texture's pixel size. When a
/// texture changes on disk and the shared <see cref="ThumbnailService"/> drops it, only the chains drawn from that texture
/// may be laid out again.
/// </summary>
public class FilmstripRendererTextureReloadTests
{
    private static string WritePng(string folder, string name, int width, int height)
    {
        string path = Path.Combine(folder, name);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void WriteAchx(string folder, string name, string chain, string texture)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var save = new AnimationChainSave { Name = chain };
        save.Frames.Add(new AnimationFrameSave
        {
            TextureName = texture, FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        list.AnimationChains.Add(save);
        list.Save(Path.Combine(folder, name));
    }

    [Fact]
    public void InvalidatingATexture_LaysOutOnlyTheChainsDrawnFromIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string sheetA = WritePng(folder, "a.png", 64, 64);
        WritePng(folder, "b.png", 64, 64);
        WriteAchx(folder, "a.achx", "A", "a.png");
        WriteAchx(folder, "b.achx", "B", "b.png");
        var save = new SequenceListSave { Sources = { "a.achx", "b.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "S" });
        string seqj = Path.Combine(folder, "s.seqj");
        save.Save(seqj);
        var document = SequenceDocument.Load(seqj);
        var thumbnails = new ThumbnailService(new TestServices().ProjectManager);
        var renderer = new FilmstripRenderer { Thumbnails = thumbnails };
        var chainA = document.FindChain(new SequenceChainReferenceSave { Source = "a.achx", Chain = "A" })!;
        var chainB = document.FindChain(new SequenceChainReferenceSave { Source = "b.achx", Chain = "B" })!;
        var cellsA = renderer.CellsFor(document, "a.achx", chainA, 0.1, 100, 40);
        var cellsB = renderer.CellsFor(document, "b.achx", chainB, 0.1, 100, 40);
        Assert.Equal(40, cellsA[0].SpriteWidth, 6);

        WritePng(folder, "a.png", 128, 64);
        thumbnails.InvalidatePath(sheetA);

        Assert.Equal(80, renderer.CellsFor(document, "a.achx", chainA, 0.1, 100, 40)[0].SpriteWidth, 6);
        Assert.Same(cellsB, renderer.CellsFor(document, "b.achx", chainB, 0.1, 100, 40));
    }
}
