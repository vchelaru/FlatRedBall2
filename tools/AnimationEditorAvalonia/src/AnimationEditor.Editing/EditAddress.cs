using System.Globalization;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>A resolved item, or the reason it could not be resolved.</summary>
public sealed record AddressResult<T>(T? Value, string? Error) where T : class
{
    public bool Ok => Value is not null;
}

/// <summary>A frame event addressed by its position; the index is not checked against the frame's events.</summary>
public sealed record FrameEventRef(AnimationChainSave Chain, AnimationFrameSave Frame, int Index);

/// <summary>
/// Text addresses for the items of an animation file, stable across runs (#1811): a chain by name
/// (<c>Walk</c>), a frame by zero-based index (<c>Walk/2</c>), a shape by frame index, type and name
/// (<c>Walk/2/rect/Hitbox</c>, type is rect, circle or polygon), a frame event by frame and event index
/// (<c>Walk/2/event/0</c>). Segments are split on '/', so a chain or
/// shape name containing '/' cannot be addressed and is refused; rename it in the editor.
/// </summary>
public static class EditAddress
{
    public static AddressResult<AnimationChainSave> ResolveChain(AnimationChainListSave doc, string address)
    {
        if (address.Length == 0) return Chain(null, "A chain address cannot be empty.");
        if (address.Contains('/')) return Chain(null, $"'{address}' is not a chain name: names containing '/' cannot be addressed.");
        return FindChain(doc, address);
    }

    public static AddressResult<AnimationFrameSave> ResolveFrame(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 2) return Frame(null, $"'{address}' is not a frame address; expected Chain/index.");
        return FrameAt(doc, parts[0], parts[1]);
    }

    public static AddressResult<ShapeSave> ResolveShape(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 4) return Shape(null, $"'{address}' is not a shape address; expected Chain/index/type/name.");

        var frame = FrameAt(doc, parts[0], parts[1]);
        if (!frame.Ok) return Shape(null, frame.Error);

        var type = parts[2];
        if (type is not ("rect" or "circle" or "polygon"))
            return Shape(null, $"Shape type '{type}' is not one of rect|circle|polygon.");

        var name = parts[3];
        var shapes = frame.Value!.ShapesSave?.Shapes.OfType<ShapeSave>() ?? [];
        var match = shapes.FirstOrDefault(s => s.Name == name && TypeName(s) == type);
        return match is null
            ? Shape(null, $"No {type} named '{name}' in frame {parts[1]} of '{parts[0]}'.")
            : new AddressResult<ShapeSave>(match, null);
    }

    /// <summary>Resolves <c>Chain/frameIndex/event/eventIndex</c> to the frame and the event's position in it.</summary>
    public static AddressResult<FrameEventRef> ResolveEvent(AnimationChainListSave doc, string address)
    {
        var parts = address.Split('/');
        if (parts.Length != 4 || parts[2] != "event")
            return new(null, $"'{address}' is not an event address; expected Chain/frameIndex/event/index.");

        var frame = FrameAt(doc, parts[0], parts[1]);
        if (!frame.Ok) return new(null, frame.Error);
        if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            return new(null, $"Event index '{parts[3]}' is not a zero-based index.");
        var chain = FindChain(doc, parts[0]).Value!;
        return new AddressResult<FrameEventRef>(new FrameEventRef(chain, frame.Value!, index), null);
    }

    /// <summary>The address type word of <paramref name="shape"/>.</summary>
    public static string TypeName(ShapeSave shape) => shape switch
    {
        AARectSave => "rect",
        CircleSave => "circle",
        PolygonSave => "polygon",
        _ => "shape",
    };

    private static AddressResult<AnimationChainSave> FindChain(AnimationChainListSave doc, string name)
    {
        var matches = doc.AnimationChains.Where(c => c.Name == name).ToList();
        return matches.Count switch
        {
            0 => Chain(null, $"No chain named '{name}'."),
            1 => new AddressResult<AnimationChainSave>(matches[0], null),
            _ => Chain(null, $"More than one chain is named '{name}'; rename them in the editor first."),
        };
    }

    private static AddressResult<AnimationFrameSave> FrameAt(AnimationChainListSave doc, string chainName, string indexText)
    {
        var chain = ResolveChain(doc, chainName);
        if (!chain.Ok) return Frame(null, chain.Error);
        if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            return Frame(null, $"Frame index '{indexText}' is not a zero-based index.");
        var frames = chain.Value!.Frames;
        return index >= frames.Count
            ? Frame(null, $"Frame index {index} is out of range: '{chainName}' has {frames.Count} frame(s).")
            : new AddressResult<AnimationFrameSave>(frames[index], null);
    }

    private static AddressResult<AnimationChainSave> Chain(AnimationChainSave? v, string? e) => new(v, e);
    private static AddressResult<AnimationFrameSave> Frame(AnimationFrameSave? v, string? e) => new(v, e);
    private static AddressResult<ShapeSave> Shape(ShapeSave? v, string? e) => new(v, e);
}
