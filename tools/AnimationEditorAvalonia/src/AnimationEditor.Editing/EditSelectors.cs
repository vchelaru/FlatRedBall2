using System.Globalization;
using System.Text.RegularExpressions;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Addresses a selector expanded to, or why it could not be expanded.</summary>
public sealed record SelectorExpansion(IReadOnlyList<string>? Targets, string? Error, EditErrorCode Code = EditErrorCode.Unspecified)
{
    public bool Ok => Targets is not null;
}

/// <summary>
/// Bulk addressing (#1811). A chain may be a glob (<c>Walk*</c>, <c>*</c>, <c>W?lk</c>) and a frame index may be
/// <c>*</c>, a number, a range (<c>2-5</c>) or a comma list of those (<c>0,3,7</c>, <c>0,2-3,6</c>), so
/// <c>Walk*/0,2-3</c> addresses several frames of several chains. A selector expands to plain addresses in chain
/// order, then in the order the indexes are written (duplicates dropped). A selector matching nothing is an error
/// (<see cref="EditErrorCode.NoMatch"/>), never a silent no-op, and any index past a chain's end is an error.
/// </summary>
public static class EditSelectors
{
    private static readonly string[] ShapeTypes = ["rect", "circle", "polygon"];

    /// <summary>True when <paramref name="text"/> uses glob characters.</summary>
    public static bool IsGlob(string text) => text.AsSpan().IndexOfAny('*', '?') >= 0;

    /// <summary>True when <paramref name="indexText"/> is anything but a single plain number.</summary>
    public static bool IsIndexSelector(string indexText) => indexText.Length == 0 || !indexText.All(char.IsAsciiDigit);

    public static SelectorExpansion ExpandChains(AnimationChainListSave doc, string pattern)
    {
        if (pattern.Length == 0) return Fail("A chain address cannot be empty.", EditErrorCode.InvalidAddress);
        if (pattern.Contains('/')) return Fail($"'{pattern}' is not a chain name: names containing '/' cannot be addressed.", EditErrorCode.InvalidAddress);

        var names = IsGlob(pattern)
            ? doc.AnimationChains.Select(c => c.Name).Where(n => GlobMatches(pattern, n)).Distinct().ToList()
            : doc.AnimationChains.Select(c => c.Name).Where(n => n == pattern).Distinct().ToList();
        return names.Count == 0
            ? Fail($"No chain matches '{pattern}'.", EditErrorCode.NoMatch)
            : new SelectorExpansion(names, null);
    }

    public static SelectorExpansion ExpandFrames(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 2) return Fail($"'{address}' is not a frame address; expected Chain/index.", EditErrorCode.InvalidAddress);

        var chains = ExpandChains(doc, parts[0]);
        if (!chains.Ok) return chains;

        var targets = new List<string>();
        foreach (var name in chains.Targets!)
        {
            // Checked here because a chain without frames, or frames without the shape or event, leaves no target to fail on later.
            var resolved = EditAddress.ResolveChain(doc, name);
            if (resolved.Code == EditErrorCode.Ambiguous) return Fail(resolved.Error!, resolved.Code);
            var chain = doc.AnimationChains.First(c => c.Name == name);
            var indexes = ParseIndexes(parts[1], chain.Frames.Count, name);
            if (indexes.Error is not null) return Fail(indexes.Error, indexes.Code);
            targets.AddRange(indexes.Indexes!.Select(i => $"{name}/{i.ToString(CultureInfo.InvariantCulture)}"));
        }
        return targets.Count == 0
            ? Fail($"'{address}' matches no frames.", EditErrorCode.NoMatch)
            : new SelectorExpansion(targets, null);
    }

    /// <summary>
    /// Expands <c>Chain/frameSelector/type/name</c>. The name may be a glob. Frames without a matching shape are
    /// skipped; matching no shape at all is an error.
    /// </summary>
    public static SelectorExpansion ExpandShapes(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 4) return Fail($"'{address}' is not a shape address; expected Chain/index/type/name.", EditErrorCode.InvalidAddress);
        var type = parts[2];
        if (!ShapeTypes.Contains(type)) return Fail($"Shape type '{type}' is not one of rect|circle|polygon.", EditErrorCode.InvalidAddress);

        var frames = ExpandFrames(doc, $"{parts[0]}/{parts[1]}");
        if (!frames.Ok) return frames;

        var targets = new List<string>();
        foreach (var frameAddress in frames.Targets!)
        {
            var split = frameAddress.LastIndexOf('/');
            var chain = doc.AnimationChains.First(c => c.Name == frameAddress[..split]);
            var frame = chain.Frames[int.Parse(frameAddress[(split + 1)..], CultureInfo.InvariantCulture)];
            var shapes = frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];
            foreach (var shape in shapes.Where(s => EditAddress.TypeName(s) == type && NameMatches(parts[3], s.Name)))
                targets.Add($"{frameAddress}/{type}/{shape.Name}");
        }
        return targets.Count == 0
            ? Fail($"'{address}' matches no shapes.", EditErrorCode.NoMatch)
            : new SelectorExpansion(targets, null);
    }

    /// <summary>
    /// Expands <c>Chain/frameSelector/event/indexSelector</c>. The event index takes the same forms as a frame index,
    /// in the order written. Events a frame does not have are skipped; matching no event at all is an error.
    /// </summary>
    public static SelectorExpansion ExpandEvents(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 4 || parts[2] != "event")
            return Fail($"'{address}' is not an event address; expected Chain/frameIndex/event/index.", EditErrorCode.InvalidAddress);

        var frames = ExpandFrames(doc, $"{parts[0]}/{parts[1]}");
        if (!frames.Ok) return frames;

        var targets = new List<string>();
        foreach (var frameAddress in frames.Targets!)
        {
            var split = frameAddress.LastIndexOf('/');
            var chain = doc.AnimationChains.First(c => c.Name == frameAddress[..split]);
            var frame = chain.Frames[int.Parse(frameAddress[(split + 1)..], CultureInfo.InvariantCulture)];
            var indexes = ParseIndexes(parts[3], frame.Events.Count, frameAddress, "event index", skipMissing: true);
            if (indexes.Error is not null) return Fail(indexes.Error, indexes.Code);
            targets.AddRange(indexes.Indexes!.Select(i => $"{frameAddress}/event/{i.ToString(CultureInfo.InvariantCulture)}"));
        }
        return targets.Count == 0
            ? Fail($"'{address}' matches no events.", EditErrorCode.NoMatch)
            : new SelectorExpansion(targets, null);
    }

    private static bool NameMatches(string pattern, string name) => IsGlob(pattern) ? GlobMatches(pattern, name) : pattern == name;

    internal static bool GlobMatches(string pattern, string text) =>
        Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.Singleline);

    internal static (IReadOnlyList<int>? Indexes, string? Error, EditErrorCode Code) ParseIndexes(string text, int count, string chainName, string noun = "frame", bool skipMissing = false)
    {
        if (text == "*") return (Enumerable.Range(0, count).ToList(), null, EditErrorCode.Unspecified);

        var result = new List<int>();
        foreach (var item in text.Split(','))
        {
            var range = item.Split('-');
            if (range.Length > 2 || range.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit) || p.Length > 9))
                return (null, $"'{text}' is not a valid {noun} selector: use *, a number, a range like 2-5, or a list like 0,3,7.", EditErrorCode.InvalidAddress);

            var from = int.Parse(range[0], CultureInfo.InvariantCulture);
            var to = int.Parse(range[^1], CultureInfo.InvariantCulture);
            if (to < from)
                return (null, $"The range '{item}' runs backwards; write it low to high.", EditErrorCode.InvalidAddress);
            if (to >= count && skipMissing) to = count - 1;
            if (to >= count)
                return (null, $"{char.ToUpperInvariant(noun[0])}{noun[1..]} index {to} is out of range: '{chainName}' has {count} {noun}(s).", EditErrorCode.IndexOutOfRange);
            for (int i = from; i <= to; i++)
                if (!result.Contains(i)) result.Add(i);
        }
        return (result, null, EditErrorCode.Unspecified);
    }

    private static SelectorExpansion Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) => new(null, message, code);
}

/// <summary>One concrete operation an op expanded to; <see cref="Target"/> names it when the op was a selector.</summary>
internal sealed record ExpandedOp(EditOp Op, string? Target);

/// <summary>Turns an op whose address fields are selectors into one op per target (#1811).</summary>
internal static class SelectorExpander
{
    private static readonly HashSet<string> ChainOps = ["delete-chain", "set-chain-loop", "set-chain-pingpong", "set-chain-reverse", "set-chain-locked", "set-chain-owner-tile",
        "set-chain-length", "invert-frame-order", "justify-offsets-bottom"];

    private static readonly HashSet<string> FrameOps =
    [
        "set-frame-length", "set-frame-region", "set-frame-offset", "set-frame-color", "set-frame-flip", "set-frame-texture",
        "delete-frame", "duplicate-frame", "add-frame-event", "set-frame-keyframe", "set-frame-keyframe-curve",
        "clear-frame-color", "add-shape",
    ];

    /// <summary>Frame ops that shift the indexes after the target, so targets run from the highest index down.</summary>
    private static readonly HashSet<string> ShiftingOps = ["delete-frame", "duplicate-frame"];

    private static readonly HashSet<string> EventOps = ["set-frame-event", "delete-frame-event", "duplicate-frame-event"];

    private static readonly HashSet<string> ShapeOps =
    [
        "delete-shape", "duplicate-shape", "set-rect", "match-frame-size", "set-circle", "set-polygon", "set-shape-common",
        "set-shape-keyframe", "set-shape-keyframe-curve",
    ];

    /// <summary>The op itself when it names one thing, otherwise one op per target. Failure is a message and code.</summary>
    public static (IReadOnlyList<ExpandedOp>? Ops, string? Error, EditErrorCode Code) Expand(AnimationChainListSave doc, EditOp op)
    {
        var name = op.Spec.Name;
        if (ChainOps.Contains(name) && EditSelectors.IsGlob(op.Values["chain"]))
            return Build(op, "chain", EditSelectors.ExpandChains(doc, op.Values["chain"]), t => t);

        // With a destination every selected frame is one block, so the op resolves the selector itself.
        var isBlock = name == "duplicate-frame" && (op.Values.ContainsKey("to") || op.Values.ContainsKey("index"));
        if (!isBlock && FrameOps.Contains(name) && IsFrameSelector(op.Values["frame"]))
        {
            var expansion = EditSelectors.ExpandFrames(doc, op.Values["frame"]);
            if (expansion.Ok && ShiftingOps.Contains(name))
                expansion = new SelectorExpansion(HighestFirst(expansion.Targets!), null);
            return Build(op, "frame", expansion, t => t);
        }

        if (EventOps.Contains(name))
        {
            var parts = op.Values["event"].Split('/');
            if (parts.Length == 4 && parts[2] == "event" && (IsFrameSelector($"{parts[0]}/{parts[1]}") || EditSelectors.IsIndexSelector(parts[3])))
            {
                var expansion = EditSelectors.ExpandEvents(doc, op.Values["event"]);
                if (expansion.Ok && name == "delete-frame-event")
                    expansion = new SelectorExpansion(HighestFirst(expansion.Targets!), null);
                return Build(op, "event", expansion, t => t);
            }
        }

        if (ShapeOps.Contains(name))
        {
            var parts = op.Values["shape"].Split('/');
            if (parts.Length == 4 && (IsFrameSelector($"{parts[0]}/{parts[1]}") || EditSelectors.IsGlob(parts[3])))
                return Build(op, "shape", EditSelectors.ExpandShapes(doc, op.Values["shape"]), t => t);
        }

        return ([new ExpandedOp(op, null)], null, EditErrorCode.Unspecified);
    }

    /// <summary>The frames a frame address or frame selector names, in selector order.</summary>
    public static (IReadOnlyList<FrameRef>? Frames, string? Error, EditErrorCode Code) ResolveFrames(AnimationChainListSave doc, string address)
    {
        IReadOnlyList<string> addresses = [address];
        if (IsFrameSelector(address))
        {
            var expansion = EditSelectors.ExpandFrames(doc, address);
            if (!expansion.Ok) return (null, expansion.Error, expansion.Code);
            addresses = expansion.Targets!;
        }

        var frames = new List<FrameRef>();
        foreach (var one in addresses)
        {
            var frame = EditAddress.ResolveFrame(doc, one);
            if (!frame.Ok) return (null, frame.Error, frame.Code);
            frames.Add(new FrameRef(EditAddress.ResolveChain(doc, one.Split('/')[0]).Value!, frame.Value!));
        }
        return (frames, null, EditErrorCode.Unspecified);
    }

    private static bool IsFrameSelector(string address)
    {
        var parts = address.Split('/');
        return parts.Length == 2 && (EditSelectors.IsGlob(parts[0]) || EditSelectors.IsIndexSelector(parts[1]));
    }

    private static (IReadOnlyList<ExpandedOp>? Ops, string? Error, EditErrorCode Code) Build(
        EditOp op, string field, SelectorExpansion expansion, Func<string, string> toValue)
    {
        if (!expansion.Ok) return (null, expansion.Error, expansion.Code);
        var ops = expansion.Targets!.Select(target =>
        {
            var values = new Dictionary<string, string>(op.Values) { [field] = toValue(target) };
            return new ExpandedOp(new EditOp(op.Index, op.Spec, values), target);
        }).ToList();
        return (ops, null, EditErrorCode.Unspecified);
    }

    /// <summary>Addresses ending in an index, reordered so each parent's items run from the last to the first.</summary>
    private static IReadOnlyList<string> HighestFirst(IReadOnlyList<string> addresses) =>
        addresses
            .GroupBy(a => a[..a.LastIndexOf('/')])
            .SelectMany(g => g.OrderByDescending(a => int.Parse(a[(a.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture)))
            .ToList();
}
