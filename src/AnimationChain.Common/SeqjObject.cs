using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One JSON object of a <c>.seqj</c> being read, with its path (<c>sequences[0].tracks[1]</c>). Every typed read of
/// untrusted JSON goes through here, so a field of the wrong kind is always a <see cref="JsonException"/> naming the
/// property (<c>sequences[0].name must be a string</c>), never the <see cref="InvalidOperationException"/> that
/// <c>JsonNode.GetValue</c> throws. A property that is absent takes its fallback; one that is present must have the
/// right kind, and JSON <c>null</c> is the wrong kind.
/// </summary>
internal readonly struct SeqjObject(JsonObject node, string path)
{
    public JsonObject Node { get; } = node;

    /// <summary>The root object, or a <see cref="JsonException"/> when the file holds something else.</summary>
    public static SeqjObject Root(JsonNode? node) =>
        node is JsonObject root
            ? new SeqjObject(root, "")
            : throw new JsonException("A .seqj file must contain a JSON object.");

    public string String(string key, string fallback = "") =>
        Read(key, node => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null, "a string", fallback);

    public bool Bool(string key, bool fallback = false) =>
        Read(key, node => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : (bool?)null, "true or false", fallback);

    public float Float(string key, float fallback = 0) =>
        Read(key, node => node is JsonValue value && value.TryGetValue<float>(out var number) && float.IsFinite(number) ? number : (float?)null,
            "a number", fallback);

    public double Double(string key, double fallback = 0) =>
        Read(key, node => node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : (double?)null,
            "a number", fallback);

    public int Int(string key, int fallback = 0) =>
        Read(key, node => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : (int?)null, "a whole number", fallback);

    /// <summary>The objects in the array under <paramref name="key"/>; empty when absent. Every element must be an object.</summary>
    public List<SeqjObject> Objects(string key)
    {
        var result = new List<SeqjObject>();
        if (!Node.TryGetPropertyValue(key, out var node)) return result;
        var array = node as JsonArray ?? throw Wrong(PathOf(key), "an array");
        for (int i = 0; i < array.Count; i++)
        {
            var itemPath = $"{PathOf(key)}[{i}]";
            result.Add(array[i] is JsonObject item ? new SeqjObject(item, itemPath) : throw Wrong(itemPath, "an object"));
        }
        return result;
    }

    /// <summary>The strings in the array under <paramref name="key"/>; empty when absent. Every element must be a string.</summary>
    public List<string> Strings(string key)
    {
        var result = new List<string>();
        if (!Node.TryGetPropertyValue(key, out var node)) return result;
        var array = node as JsonArray ?? throw Wrong(PathOf(key), "an array");
        for (int i = 0; i < array.Count; i++)
        {
            result.Add(array[i] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : throw Wrong($"{PathOf(key)}[{i}]", "a string"));
        }
        return result;
    }

    /// <summary>A whole number of at least 1, the shape of the top-level <c>"version"</c>.</summary>
    public int PositiveInt(string key, int fallback)
    {
        var value = Int(key, fallback);
        return value >= 1 ? value : throw Wrong(PathOf(key), "a whole number of at least 1");
    }

    private string PathOf(string key) => path.Length == 0 ? key : $"{path}.{key}";

    private T Read<T>(string key, Func<JsonNode?, T?> convert, string expected, T fallback) where T : struct =>
        Node.TryGetPropertyValue(key, out var node) ? convert(node) ?? throw Wrong(PathOf(key), expected) : fallback;

    // The string reads need a class-typed overload of the same shape.
    private string Read(string key, Func<JsonNode?, string?> convert, string expected, string fallback) =>
        Node.TryGetPropertyValue(key, out var node) ? convert(node) ?? throw Wrong(PathOf(key), expected) : fallback;

    private static JsonException Wrong(string path, string expected) => new($"{path} must be {expected}.");
}
