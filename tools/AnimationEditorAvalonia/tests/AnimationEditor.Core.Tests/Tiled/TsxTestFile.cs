using AnimationEditor.Core.Tiled;
using DotTiled;

namespace AnimationEditor.Core.Tests.Tiled;

internal static class TsxTestFile
{
    /// <summary>Writes the tiles of <paramref name="tileset"/> that differ from the file at <paramref name="path"/>.</summary>
    public static void Write(Tileset tileset, string path)
    {
        var source = TsxSource.Load(path);
        TsxWriter.Write(source, TsxWriter.Render(source, TsxLoader.ReadTileset(source), tileset), source.Path);
    }
}
