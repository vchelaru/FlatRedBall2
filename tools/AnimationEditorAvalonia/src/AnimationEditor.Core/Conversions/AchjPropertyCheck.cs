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

    // By the property's path without indexes, such as "/animationChains/frames/relativeX".
    private readonly Dictionary<string, bool> _isRead = new();

    private AchjPropertyCheck(JsonObject source) => _source = source;

    /// <summary>
    /// The path of the first property of <paramref name="source"/> that <paramref name="list"/>, read from it, does not hold:
    /// "animationChains[0].custom", or null. A property without any content holds nothing to lose.
    /// </summary>
    public static string? FirstUnread(JsonObject source, AnimationChainListSave list)
    {
        var copy = (JsonObject)source.DeepClone();
        return new AchjPropertyCheck(copy).FirstUnread(copy, JsonNode.Parse(list.ToJsonString())!, "");
    }

    private string? FirstUnread(JsonNode source, JsonNode written, string path)
    {
        // A null item is skipped when it is read.
        if (source is JsonArray items && written is JsonArray writtenItems)
            return items.OfType<JsonNode>().Zip(writtenItems, (item, other) => FirstUnread(item, other!, path)).FirstOrDefault(unread => unread is not null);
        if (source is not JsonObject properties || written is not JsonObject writtenProperties) return null;

        foreach (var (name, value) in properties.ToList())
        {
            if (value is null) continue;
            string inside = $"{path}/{name}";
            // What is written back is compared inside. What is not is a default, left out, when it is read at all.
            if (writtenProperties[name] is { } other)
            {
                if (FirstUnread(value, other, inside) is { } unread) return unread;
            }
            else if (HasContent(value) && !IsRead(value, name, inside))
            {
                return value.GetPath()[2..];
            }
        }
        return null;
    }

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
