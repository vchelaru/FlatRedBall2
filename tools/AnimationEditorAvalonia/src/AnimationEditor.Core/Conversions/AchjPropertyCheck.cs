using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace AnimationEditor.Core.Conversions;

/// <summary>Finds the parts of an <c>.achj</c> text that reading it leaves out, as <see cref="AchxElementCheck"/> does for an <c>.achx</c>.</summary>
public sealed class AchjPropertyCheck
{
    private readonly JsonObject _source;
    private readonly List<ConversionDrop> _drops = new();

    // By the property's path without indexes, such as "/animationChains/frames/relativeX".
    private readonly Dictionary<string, bool> _isRead = new();

    private AchjPropertyCheck(JsonObject source) => _source = source;

    /// <summary>
    /// Every property of <paramref name="source"/> that <paramref name="list"/>, read from it, does not hold. One without any
    /// content holds nothing to lose.
    /// </summary>
    public static IReadOnlyList<ConversionDrop> Drops(JsonObject source, AnimationChainListSave list)
    {
        var copy = (JsonObject)source.DeepClone();
        var check = new AchjPropertyCheck(copy);
        check.AddDrops(copy, JsonNode.Parse(list.ToJsonString())!, "", default);
        return check._drops;
    }

    private void AddDrops(JsonNode source, JsonNode written, string path, ConversionPlace place)
    {
        if (source is JsonArray items && written is JsonArray writtenItems)
        {
            int number = 0;
            // A null item is skipped when it is read.
            foreach (var (item, other) in items.OfType<JsonNode>().Zip(writtenItems))
                AddDrops(item, other!, path, PlaceOf(other!, ++number, path, place));
            return;
        }
        if (source is not JsonObject properties || written is not JsonObject writtenProperties) return;

        foreach (var (name, value) in properties.ToList())
        {
            if (value is null) continue;
            string inside = $"{path}/{name}";
            // What is written back is compared inside. What is not is a default, left out, when it is read at all.
            if (writtenProperties[name] is { } other)
                AddDrops(value, other, inside, place);
            else if (HasContent(value) && !IsRead(value, name, inside))
                _drops.Add(new ConversionDrop($"\"{name}\"", place));
        }
    }

    private static ConversionPlace PlaceOf(JsonNode item, int number, string path, ConversionPlace outer) => path switch
    {
        "/animationChains" => new ConversionPlace(number, (string)item["name"]!, 0),
        "/animationChains/frames" => outer with { Frame = number },
        _ => outer,
    };

    private static bool HasContent(JsonNode value) => value switch
    {
        JsonObject properties => properties.Count > 0,
        JsonArray items => items.Count > 0,
        JsonValue text when text.TryGetValue(out string? content) => content.Length > 0,
        _ => true,
    };

    private bool IsRead(JsonNode value, string name, string path)
    {
        if (_isRead.TryGetValue(path, out bool known)) return known;

        // A property is read when some value of it makes a difference, which one of the wrong kind does too.
        var parent = (JsonObject)value.Parent!;
        string? without = Read(null);
        bool isRead = without is null || Read(7) != without || Read(true) != without;
        parent[name] = value;
        return _isRead[path] = isRead;

        string? Read(JsonNode? other)
        {
            parent[name] = other;
            try
            {
                return AnimationChainListSave.FromJsonString(_source.ToJsonString()).ToJsonString();
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }
}
