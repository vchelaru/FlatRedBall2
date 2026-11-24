using System.Text.Json.Nodes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Ids on the tracks and blocks of a sequence file while a batch of ops runs, so a check warning is recognised after the
/// batch renamed, moved or reordered what it is about. <c>SequenceDocument</c> rebuilds its objects from JSON on
/// every edit, so the ids travel as unknown properties; <see cref="SequenceSession.Save"/> removes them before writing.
/// </summary>
internal static class SequenceEditIds
{
    private const string Key = "$aecliEditId";

    public static void Add(SequenceListSave save)
    {
        int next = 0;
        foreach (var properties in Holders(save)) properties[Key] = next++;
    }

    public static void Remove(SequenceListSave save)
    {
        foreach (var properties in Holders(save)) properties.Remove(Key);
    }

    /// <summary>The id, or null outside a batch and for anything the batch added.</summary>
    public static string? Of(JsonObject unknownProperties) => unknownProperties[Key]?.ToString();

    private static IEnumerable<JsonObject> Holders(SequenceListSave save)
    {
        foreach (var sequence in save.Sequences)
            foreach (var track in sequence.Tracks)
            {
                yield return track.UnknownProperties;
                if (track.Reference is not null) yield return track.Reference.UnknownProperties;
                foreach (var block in track.Chains) yield return block.UnknownProperties;
            }
    }
}
