using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Utilities;
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
        return chain is null ? Fail(EditErrorCode.Refused, $"Could not add chain '{name}'.") : Ok(chain.Name);
    }

    public static EditResult DeleteChain(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain)
    {
        if (!Contains(projectManager, chain)) return Fail(EditErrorCode.NotFound, $"Chain '{chain.Name}' is not in the file.");
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
        if (!Contains(projectManager, source)) return Fail(EditErrorCode.NotFound, $"Chain '{source.Name}' is not in the file.");

        if (newName is not null)
        {
            newName = newName.Trim();
            var check = CheckNewChainName(projectManager, newName);
            if (check is not null) return check;
        }

        var copy = commands.DuplicateChain(source, ChainFlip.None, newName);
        return copy is null ? Fail(EditErrorCode.Refused, $"Could not duplicate chain '{source.Name}'.") : Ok(copy.Name);
    }

    /// <summary>
    /// Appends copies of the chains of the .achx/.achj <paramref name="sourcePath"/> that <paramref name="pattern"/> (a name
    /// or glob) matches, in that file's order and as one undo step. Textures are referenced where they are, and lengths are
    /// converted to the receiving file's unit. <paramref name="newName"/> renames a single copy. A name already in use fails
    /// the whole import. The source file is only read.
    /// </summary>
    public static EditResult ImportChains(
        IProjectManager projectManager, IAppCommands commands, string sourcePath, string pattern, string? newName)
    {
        var sourceName = Path.GetFileName(sourcePath);
        if (Path.GetExtension(sourcePath).ToLowerInvariant() is not (".achx" or ".achj"))
            return Fail(EditErrorCode.UnsupportedFile, $"Chains can only be imported from an .achx or .achj: {sourcePath}");
        if (!File.Exists(sourcePath)) return Fail(EditErrorCode.NotFound, $"File not found: {sourcePath}");
        if (projectManager.FileName is { } current && new FilePath(sourcePath) == new FilePath(current))
            return Fail(EditErrorCode.Refused, $"{sourceName} is the file being edited; use duplicate-chain to copy a chain inside one file.");

        var read = AnimationFileReader.Read(sourcePath);
        if (read.Document is not { } source) return Fail(EditErrorCode.IoFailed, read.Error!);

        List<AnimationChainSave> chains;
        if (EditSelectors.IsGlob(pattern))
        {
            var names = EditSelectors.ExpandChains(source, pattern);
            if (!names.Ok) return Fail(names.Code, $"{sourceName}: {names.Error}");
            chains = source.AnimationChains.Where(c => names.Targets!.Contains(c.Name)).ToList();
            var twice = chains.GroupBy(c => c.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (twice.Count > 0)
                return Fail(EditErrorCode.Ambiguous, $"{sourceName}: more than one chain is named {Quoted(twice)}; rename them in the editor first.");
        }
        else
        {
            var chain = EditAddress.ResolveChain(source, pattern);
            if (!chain.Ok) return Fail(chain.Code, $"{sourceName}: {chain.Error}");
            chains = [chain.Value!];
        }

        if (newName is not null)
        {
            if (chains.Count > 1)
                return Fail(EditErrorCode.InvalidValue, $"name is for a single chain, but '{pattern}' matches {chains.Count} chains.");
            newName = newName.Trim();
            if (CheckNewChainName(projectManager, newName) is { } refused) return refused;
            chains[0].Name = newName;
        }

        var document = projectManager.AnimationChainListSave!;
        var taken = document.AnimationChains.Select(c => c.Name).ToHashSet();
        var clashes = chains.Where(c => taken.Contains(c.Name)).Select(c => c.Name).ToList();
        if (clashes.Count > 0)
            return Fail(EditErrorCode.DuplicateName,
                $"The file already has chains named {Quoted(clashes)}; rename them, or import one chain at a time with name=<new name>.");

        // Only the chains that come along need their textures to convert a UV file.
        source.AnimationChains = chains;
        try { AnimationFileLoader.ConvertToEditorCoordinates(source, new FilePath(sourcePath)); }
        catch (InvalidOperationException ex) { return Fail(EditErrorCode.UnsupportedFile, $"{sourceName}: {ex.Message}"); }

        ClipboardPayload.Retarget(chains, Path.GetDirectoryName(sourcePath)!, FrameLengthUnit.Of(source), FrameLengthUnit.Of(document));
        var folder = Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;
        foreach (var frame in chains.SelectMany(c => c.Frames))
            frame.TextureName = TexturePathHelper.NormalizeStoredTextureName(frame.TextureName, folder);

        commands.PasteChains(chains, document.AnimationChains.Count);
        return Ok([.. chains.Select(c => c.Name)]);
    }

    private static string Quoted(IEnumerable<string> names) => string.Join(", ", names.Select(n => $"'{n}'"));

    /// <summary>Moves <paramref name="chain"/> so it ends up at zero-based <paramref name="index"/>.</summary>
    public static EditResult ReorderChain(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, int index)
    {
        var chains = projectManager.AnimationChainListSave?.AnimationChains;
        var current = chains?.IndexOf(chain) ?? -1;
        if (chains is null || current < 0) return Fail(EditErrorCode.NotFound, $"Chain '{chain.Name}' is not in the file.");
        if (index < 0 || index >= chains.Count) return OutOfRange(index, chains.Count, "chain");

        if (index == current) return Ok();

        commands.MoveChain(chain, index - current);
        return Ok(chain.Name);
    }

    /// <summary>
    /// Appends a frame showing <paramref name="absoluteTexture"/>, stored relative to the .achx. The file must
    /// exist and pass <paramref name="probe"/>. Without <paramref name="region"/> the frame shows the whole texture, whose
    /// size must be readable; a region needs the texture's size, which is only known for PNG files. Without <paramref name="length"/> the
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
            return Fail(EditErrorCode.InvalidValue, "A frame length must be greater than zero.");
        if (length is { } given && !NumberFields.InRange(given))
            return Fail(EditErrorCode.InvalidValue, $"A frame length must be at most {NumberFields.Max}.");

        if (region is { } r)
        {
            var read = TextureCoordinateConverter.TryReadPngSize(absoluteTexture);
            if (read is null)
                return Fail(EditErrorCode.UnsupportedFile, "A region needs the texture's size, which is only read from PNG files.");
            var size = read.Value;
            if (r.X < 0 || r.Y < 0 || r.Width <= 0 || r.Height <= 0 || r.X > size.Width - r.Width || r.Y > size.Height - r.Height)
                return Fail(EditErrorCode.InvalidValue,
                    $"The region {r.X},{r.Y},{r.Width},{r.Height} is not inside the {size.Width}x{size.Height} texture.");
        }
        else if (FrameTextureEdit.CheckWholeTexture(commands, absoluteTexture) is { } refused)
        {
            return refused;
        }

        var folder = string.IsNullOrEmpty(projectManager.FileName) ? string.Empty : Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;
        var stored = folder.Length == 0 ? absoluteTexture : TexturePathHelper.ComputeStorePath(absoluteTexture, folder);

        if (region is { } rect)
            commands.AddFrameFromPixelBounds(chain, stored, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
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
            return Fail(EditErrorCode.InvalidValue, "cellWidth, cellHeight, columns and rows must all be greater than zero.");
        if (!File.Exists(absoluteTexture))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absoluteTexture}");
        if (!probe.CanDecode(absoluteTexture))
            return Fail(EditErrorCode.TextureUndecodable, $"Could not load image: {absoluteTexture}");

        var size = TextureCoordinateConverter.TryReadPngSize(absoluteTexture);
        if (size is null)
            return Fail(EditErrorCode.UnsupportedFile, "A grid needs the texture's size, which is only read from PNG files.");
        if ((long)columns * cellWidth > size.Value.Width || (long)rows * cellHeight > size.Value.Height)
            return Fail(EditErrorCode.InvalidValue,
                $"A {columns}x{rows} grid of {cellWidth}x{cellHeight} cells does not fit the {size.Value.Width}x{size.Value.Height} texture.");

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
        if (index < 0) return Fail(EditErrorCode.NotFound, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);

        commands.DeleteFrames([frame]);
        return Ok($"{chain.Name}/{index}");
    }

    /// <summary>Inserts a copy of <paramref name="frame"/> right after it.</summary>
    public static EditResult DuplicateFrame(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame)
    {
        var index = chain.Frames.IndexOf(frame);
        if (index < 0) return Fail(EditErrorCode.NotFound, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);

        var copy = commands.DuplicateFrame(frame, chain);
        return copy is null
            ? Fail(EditErrorCode.Refused, $"Could not duplicate frame {index} of '{chain.Name}'.")
            : Ok($"{chain.Name}/{chain.Frames.IndexOf(copy)}");
    }

    /// <summary>
    /// Inserts copies of <paramref name="sources"/>, in order and as one block, at zero-based <paramref name="index"/> of
    /// <paramref name="to"/> (null: at the end). A null <paramref name="to"/> is each source's own chain. Only the
    /// destination must be unlocked.
    /// </summary>
    public static EditResult DuplicateFramesTo(
        IAppCommands commands, IReadOnlyList<FrameRef> sources, AnimationChainSave? to, int? index)
    {
        var changed = new List<string>();
        foreach (var block in sources.GroupBy(s => to ?? s.Chain))
        {
            var chain = block.Key;
            if (chain.IsLocked) return LockedFail(chain);
            var slot = index ?? chain.Frames.Count;
            if (slot < 0 || slot > chain.Frames.Count) return OutOfRange(slot, chain.Frames.Count + 1, "frame");

            commands.PasteFrames(chain, block.Select(s => s.Frame).ToList(), slot);
            changed.AddRange(Enumerable.Range(slot, block.Count()).Select(i => $"{chain.Name}/{i}"));
        }
        return Ok([.. changed]);
    }

    /// <summary>Moves <paramref name="frame"/> so it ends up at zero-based <paramref name="index"/> of its chain.</summary>
    public static EditResult ReorderFrame(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, int index)
    {
        var current = chain.Frames.IndexOf(frame);
        if (current < 0) return Fail(EditErrorCode.NotFound, $"The frame is not in chain '{chain.Name}'.");
        if (chain.IsLocked) return LockedFail(chain);
        if (index < 0 || index >= chain.Frames.Count) return OutOfRange(index, chain.Frames.Count, "frame");

        if (index == current) return Ok();

        commands.MoveFrame(frame, chain, index - current);
        return Ok($"{chain.Name}/{index}");
    }

    /// <summary>Reverses a chain's frames. A chain of one frame or none is unchanged.</summary>
    public static EditResult InvertFrameOrder(IAppCommands commands, AnimationChainSave chain)
    {
        if (chain.IsLocked) return LockedFail(chain);
        if (chain.Frames.Count < 2) return Ok();

        commands.InvertFrameOrder(chain);
        return Ok(chain.Name);
    }

    private static EditResult? CheckNewChainName(IProjectManager projectManager, string name)
    {
        if (name.Length == 0) return Fail(EditErrorCode.EmptyName, "Chain name cannot be empty.");
        if (name.Contains('/')) return Fail(EditErrorCode.InvalidAddress, $"Chain name '{name}' cannot contain '/'.");
        if (StorableText.Problem(name) is { } problem) return Fail(EditErrorCode.InvalidValue, problem);
        if (projectManager.AnimationChainListSave?.AnimationChains.Any(c => c.Name == name) == true)
            return Fail(EditErrorCode.DuplicateName, $"A chain named '{name}' already exists.");
        return null;
    }

    private static bool Contains(IProjectManager projectManager, AnimationChainSave chain) =>
        projectManager.AnimationChainListSave?.AnimationChains.Contains(chain) == true;

    private static EditResult OutOfRange(int index, int count, string what) =>
        Fail(EditErrorCode.IndexOutOfRange, $"The {what} index {index} is out of range: valid indexes are 0 to {count - 1}.");

    internal static EditResult LockedFail(AnimationChainSave chain) =>
        Fail(EditErrorCode.Locked, $"Chain '{chain.Name}' is locked; unlock it first (set-chain-locked).");

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
