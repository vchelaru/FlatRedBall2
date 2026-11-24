using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using DotTiled;
using System;
using System.IO;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// The file an open tileset is saved to. A document's captured states share one, because what is on
/// disk is not undone with an edit.
/// </summary>
internal sealed class TsxFile(FilePath path, TsxSource source, Tileset tileset)
{
    public FilePath Path { get; private set; } = path;

    /// <summary>The image as <see cref="Path"/> names it.</summary>
    public string ImageSource => TexturePathHelper.Rebase(
        _openedImageSource, _opened.Path.GetDirectoryContainingThis(), Path.GetDirectoryContainingThis());

    /// <summary>The text as opened. Every save starts from it, so an undone edit leaves the opened bytes.</summary>
    private TsxSource _opened = source;
    private Tileset _openedTileset = tileset;
    private string _openedImageSource = Layout(tileset).ImageSource;

    /// <summary>What was last read from or written to <see cref="Path"/>.</summary>
    private string _diskText = source.Text;
    private Tileset _diskTileset = tileset;

    /// <summary>What the last <see cref="Save"/> wrote, to either file.</summary>
    private string _writtenText = source.Text;

    /// <summary>A save went into a change another program made. The document has it only after a reload.</summary>
    public bool LacksOutsideChange { get; private set; }

    public void Save(Tileset tileset, FilePath target)
    {
        if (target == Path && target.Exists() && File.ReadAllText(target.FullPath) != _diskText)
        {
            // Another program, usually Tiled, wrote the file and the reload has not run yet: what this save
            // changes goes into that text.
            TsxSource disk;
            try
            {
                (disk, var diskTileset) = ProjectManager.ReadTsx(target);
                if (Layout(diskTileset) != Layout(_openedTileset) with { ImageSource = ImageSource })
                    throw new InvalidOperationException("its tile grid or image is not the open one");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(SaveTarget.UnreadableOnDiskMessage(target.FullPath, ex.Message), ex);
            }
            var merged = disk.WithText(TsxWriter.Render(disk, _diskTileset, tileset));
            TsxWriter.Write(merged, merged.Text, target);
            _openedImageSource = ImageSource;
            (_opened, _openedTileset, _diskText, _diskTileset) = (merged, tileset, merged.Text, tileset);
            _writtenText = merged.Text;
            LacksOutsideChange = true;
            return;
        }

        var text = TsxWriter.Render(_opened, _openedTileset, tileset);
        _writtenText = TsxWriter.Write(_opened, text, target);
        if (target == Path)
            (_diskText, _diskTileset) = (_writtenText, tileset);
    }

    /// <summary>What decides which tile an animation's rects are on.</summary>
    private static (int, int, int, int, int, int, string ImageSource, int, int) Layout(Tileset tileset)
    {
        var image = tileset.Image.Value;
        return (tileset.TileWidth, tileset.TileHeight, tileset.Columns, tileset.TileCount, tileset.Spacing, tileset.Margin,
            image.Source.HasValue ? image.Source.Value : string.Empty,
            image.Width.HasValue ? image.Width.Value : 0, image.Height.HasValue ? image.Height.Value : 0);
    }

    /// <summary>Makes the file a Save As just wrote <paramref name="tileset"/> to this one.</summary>
    public void SetPath(FilePath path, Tileset tileset)
    {
        (Path, _diskText, _diskTileset) = (path, _writtenText, tileset);
    }
}
