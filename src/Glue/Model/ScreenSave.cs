using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>The contents of one Glue <c>.glsj</c> file.</summary>
public class ScreenSave : GlueElement
{
    private string? _baseScreen;

    /// <summary>
    /// The screen this one derives from, in the same backslash form as <see cref="GlueElement.Name"/>.
    /// </summary>
    public string? BaseScreen
    {
        get => _baseScreen;
        set => _baseScreen = GlueSentinel.NullIfUnset(value);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A screen file carries this alongside <see cref="BaseScreen"/>, so the two agree on disk. It is
    /// still computed here, because an entity file carries only its own base member.
    /// </remarks>
    [JsonIgnore]
    public override string? BaseElement => BaseScreen;

    /// <summary>The screen to advance to when this one finishes.</summary>
    public string? NextScreen { get; set; }
}
