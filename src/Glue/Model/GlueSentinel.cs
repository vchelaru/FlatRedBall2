namespace FlatRedBall2.Glue.Model;

/// <summary>
/// Glue's "nothing chosen" placeholders, which its own save classes translate in their property
/// setters rather than in the serializer.
/// </summary>
/// <remarks>
/// Newtonsoft runs those setters on read, so FRB1 never sees the literal text.
/// <c>System.Text.Json</c> binds straight to the backing field, so the mirror has to translate
/// explicitly — otherwise a variable tunnels to an object genuinely named <c>&lt;NONE&gt;</c>.
/// </remarks>
internal static class GlueSentinel
{
    /// <summary>The placeholder Glue writes for an unset string member.</summary>
    internal const string None = "<NONE>";

    /// <summary>Collapses <see cref="None"/> and the empty string to null.</summary>
    /// <remarks>
    /// FRB1 maps the sentinel to <c>""</c> on some members and to <c>null</c> on others; both mean
    /// "unset" at every call site, so the mirror settles on null and tests emptiness nowhere.
    /// </remarks>
    internal static string? NullIfUnset(string? value) =>
        string.IsNullOrEmpty(value) || value == None ? null : value;
}
