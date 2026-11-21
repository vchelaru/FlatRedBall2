using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>A sub-rectangle of a texture in pixels.</summary>
public readonly record struct FramePixelRegion(int X, int Y, int Width, int Height);

/// <summary>
/// Adds, removes, duplicates and reorders chains and frames (#1811). Never touches selection; each
/// change is one undo step (adding a frame with a length is two). A chain or frame in a locked chain
/// fails with <see cref="EditErrorCode.Locked"/> rather than being skipped silently.
/// <see cref="EditResult.ChangedItems"/> holds the address of the item the operation produced or removed.
/// </summary>
public static class StructureEdit
{
    /// <summary>
    /// Appends a chain named <paramref name="name"/> (trimmed). Unlike the editor's own Add Animation, which
    /// de-duplicates silently, a name another chain holds fails with <see cref="EditErrorCode.DuplicateName"/>.
    /// </summary>
    public static EditResult AddChain(IProjectManager projectManager, IAppCommands commands, string name)
    {
        name = name.Trim();
        var check = CheckNewChainName(projectManager, name);
        if (check is not null) return check;

        var chain = commands.AddAnimationChainWithName(name);
        return chain is null ? Fail(EditErrorCode.Unspecified, $"Could not add chain '{name}'.") : Ok(chain.Name);
    }

    public static EditResult DeleteChain(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain)
    {
        if (!Contains(projectManager, chain)) return Fail(EditErrorCode.Unspecified, $"Chain '{chain.Name}' is not in the file.");
        if (chain.IsLocked) return LockedFail(chain);

        commands.DeleteAnimationChains([chain]);
        return Ok(chain.Name);
    }

    /// <summary>
    /// Inserts a copy of <paramref name="source"/> after it. <paramref name="newName"/> null picks the editor's
    /// numbered name (Walk becomes Walk2); a given name must be non-empty and unused.
    /// </summary>
    public static EditResult DuplicateChain(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave source, string? newName)
    {
        if (!Contains(projectManager, source)) return Fail(EditErrorCode.Unspecified, $"Chain '{source.Name}' is not in the file.");

        if (newName is not null)
        {
            newName = newName.Trim();
            var check = CheckNewChainName(projectManager, newName);
            if (check is not null) return check;
        }

        var copy = commands.DuplicateChain(source, ChainFlip.None, newName);
        return copy is null ? Fail(EditErrorCode.Unspecified, $"Could not duplicate chain '{source.Name}'.") : Ok(copy.Name);
    }

    /// <summary>Moves <paramref name="chain"/> so it ends up at zero-based <paramref name="index"/>.</summary>
    public static EditResult ReorderChain(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, int index)
    {
        var chains = projectManager.AnimationChainListSave?.AnimationChains;
        var current = chains?.IndexOf(chain) ?? -1;
        if (chains is null || current < 0) return Fail(EditErrorCode.Unspecified, $"Chain '{chain.Name}' is not in the file.");
        if (index < 0 || index >= chains.Count) return OutOfRange(index, chains.Count, "chain");

        if (index == current) return Ok();

        commands.MoveChain(chain, index - current);
        return Ok(chain.Name);
    }

    /// <summary>
    /// Appends a frame showing <paramref name="absoluteTexture"/>, stored relative to the .achx. The file must
    /// exist and pass <paramref name="probe"/>. Without <paramref name="region"/> the frame shows the whole texture;
    /// a region needs the texture's size, which is only known for PNG files. Without <paramref name="length"/> the
    /// frame gets the default length of the file's own time unit; a given length is in that unit.
    /// </summary>
    public static EditResult AddFrame(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain,
        string absoluteTexture, FramePixelRegion? region, float? length, ITextureProbe probe)
    {
        if (chain.IsLocked) return LockedFail(chain);
        if (!File.Exists(absoluteTexture))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absoluteTexture}");
        if (!probe.CanDecode(absoluteTexture))
            return Fail(EditErrorCode.TextureUndecodable, $"Could not load image: {absoluteTexture}");
        if (length is { } l && !(l > 0f))
            return Fail(EditErrorCode.Unspecified, "A frame length must be greater than zero.");

        (int W, int H) size = default;
        if (region is { } r)
        {
            var read = TextureCoordinateConverter.TryReadPngSize(absoluteTexture);
            if (read is null)
                return Fail(EditErrorCode.Unspecified, "A region needs the texture's size, which is only read from PNG files.");
            size = read.Value;
            if (r.X < 0 || r.Y < 0 || r.Width <= 0 || r.Height <= 0 || r.X + r.Width > size.W || r.Y + r.Height > size.H)
                return Fail(EditErrorCode.Unspecified,
                    $"The region {r.X},{r.Y},{r.Width},{r.Height} is not inside the {size.W}x{size.H} texture.");
        }

        var folder = string.IsNullOrEmpty(projectManager.FileName) ? string.Empty : Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;
        var stored = folder.Length == 0 ? absoluteTexture : TexturePathHelper.ComputeStorePath(absoluteTexture, folder);

        if (region is { } rect)
            commands.AddFrameFromPixelBounds(chain, stored, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height, size.W, size.H);
        else
            commands.AddFrame(chain, stored);

        var frame = chain.Frames[^1];
        if (length is { } value) commands.SetFrameLength([frame], value);
        return Ok($"{chain.Name}/{chain.Frames.Count - 1}");
    }

    /// <summary>
    /// Appends a frame for each cell of a sprite sheet grid (#1811), row by row from the top-left, starting at
    /// cell <paramref name="start"/> and taking <paramref name="count"/> cells (null: to the end of the grid). Cells are
    /// <paramref name="cellWidth"/> by <paramref name="cellHeight"/> pixels; the whole grid must lie inside the PNG
    /// texture. Everything is validated before the first frame is added. Each frame goes through
    /// <see cref="AddFrame"/>, so the texture checks, stored path and length rules are the same.
    /// </summary>
    public static EditResult AddFramesGrid(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, string absoluteTexture,
        int cellWidth, int cellHeight, int columns, int rows, int start, int? count, float? frameLength, ITextureProbe probe)
    {
        if (chain.IsLocked) return LockedFail(chain);
        if (cellWidth <= 0 || cellHeight <= 0 || columns <= 0 || rows <= 0)
            return Fail(EditErrorCode.Unspecified, "cellWidth, cellHeight, columns and rows must all be greater than zero.");
        if (!File.Exists(absoluteTexture))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absoluteTexture}");
        if (!probe.CanDecode(absoluteTexture))
            return Fail(EditErrorCode.TextureUndecodable, $"Could not load image: {absoluteTexture}");

        var size = TextureCoordinateConverter.TryReadPngSize(absoluteTexture);
        if (size is null)
            return Fail(EditErrorCode.Unspecified, "A grid needs the texture's size, which is only read from PNG files.");
        if ((long)columns * cellWidth > size.Value.W || (long)rows * cellHeight > size.Value.H)
            return Fail(EditErrorCode.Unspecified,
                $"A {columns}x{rows} grid of {cellWidth}x{cellHeight} cells does not fit the {size.Value.W}x{size.Value.H} texture.");

        var cells = columns * rows;
        if (start < 0 || start >= cells)
            return Fail(EditErrorCode.IndexOutOfRange, $"start {start} is out of range: the grid has {cells} cell(s), numbered 0 to {cells - 1} row by row.");
        var take = count ?? cells - start;
        if (take <= 0 || start + take > cells)
            return Fail(EditErrorCode.IndexOutOfRange, $"count {take} does not fit: {cells - start} cell(s) remain from start {start}.");

        var firstNew = chain.Frames.Count;
        for (int cell = start; cell < start + take; cell++)
        {
            var region = new FramePixelRegion(cell % columns * cellWidth, cell / columns * cellHeight, cellWidth, cellHeight);
            var added = AddFrame(projectManager, commands, chain, absoluteTexture, region, frameLength, probe);
            if (!added.Success) return added;
        }
        return Ok($"{chain.Name}/{firstNew}-{chain.Frames.Count - 1}");
    }

    public static EditResult DeleteFrame(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame)
    {
        var index = chain.Frames.IndexOf(frame);
        if (index < 0) return Fail(EditErrorCode.Unspecified, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);

        commands.DeleteFrames([frame]);
        return Ok($"{chain.Name}/{index}");
    }

    /// <summary>Inserts a copy of <paramref name="frame"/> right after it.</summary>
    public static EditResult DuplicateFrame(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame)
    {
        var index = chain.Frames.IndexOf(frame);
        if (index < 0) return Fail(EditErrorCode.Unspecified, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);

        var copy = commands.DuplicateFrame(frame, chain);
        return copy is null
            ? Fail(EditErrorCode.Unspecified, $"Could not duplicate frame {index} of '{chain.Name}'.")
            : Ok($"{chain.Name}/{chain.Frames.IndexOf(copy)}");
    }

    /// <summary>Moves <paramref name="frame"/> so it ends up at zero-based <paramref name="index"/> of its chain.</summary>
    public static EditResult ReorderFrame(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, int index)
    {
        var current = chain.Frames.IndexOf(frame);
        if (current < 0) return Fail(EditErrorCode.Unspecified, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);
        if (index < 0 || index >= chain.Frames.Count) return OutOfRange(index, chain.Frames.Count, "frame");

        if (index == current) return Ok();

        commands.MoveFrame(frame, chain, index - current);
        return Ok($"{chain.Name}/{index}");
    }

    private static EditResult? CheckNewChainName(IProjectManager projectManager, string name)
    {
        if (name.Length == 0) return Fail(EditErrorCode.EmptyName, "Chain name cannot be empty.");
        if (name.Contains('/')) return Fail(EditErrorCode.Unspecified, $"Chain name '{name}' cannot contain '/'.");
        if (projectManager.AnimationChainListSave?.AnimationChains.Any(c => c.Name == name) == true)
            return Fail(EditErrorCode.DuplicateName, $"A chain named '{name}' already exists.");
        return null;
    }

    private static bool Contains(IProjectManager projectManager, AnimationChainSave chain) =>
        projectManager.AnimationChainListSave?.AnimationChains.Contains(chain) == true;

    private static EditResult OutOfRange(int index, int count, string what) =>
        Fail(EditErrorCode.IndexOutOfRange, $"The {what} index {index} is out of range: valid indexes are 0 to {count - 1}.");

    internal static EditResult LockedFail(AnimationChainSave chain) =>
        Fail(EditErrorCode.Locked, $"Chain '{chain.Name}' is locked; unlock it in the editor first.");

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
