using System.Collections.Generic;
using System.Xml.Linq;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// How an .achx wrote the text elements of one object that read as empty, so that saving writes an untouched one the same
/// way: FRB1 leaves a null string out and self-closes an empty one, and this writer uses a pair of tags.
/// </summary>
internal struct EmptyTextForms
{
    // Element name to whether it was there (self-closed). An empty pair of tags, the default form, is not listed.
    private Dictionary<string, bool>? _forms;

    /// <summary>The text of element <paramref name="name"/>, empty when it is left out.</summary>
    public string Read(XElement parent, string name)
    {
        if (ReadOptional(parent, name) is { } text) return text;
        (_forms ??= new())[name] = false;
        return string.Empty;
    }

    /// <summary>The text of element <paramref name="name"/>, null when it is left out.</summary>
    public string? ReadOptional(XElement parent, string name)
    {
        var el = parent.Element(name);
        if (el is { IsEmpty: true }) (_forms ??= new())[name] = true;
        return el?.Value;
    }

    /// <summary>The element to write for <paramref name="value"/>; null when nothing is written.</summary>
    public readonly XElement? Write(string name, string? value)
    {
        if (value == null) return null;
        if (value.Length > 0 || _forms == null || !_forms.TryGetValue(name, out bool selfClosed)) return new XElement(name, value);
        return selfClosed ? new XElement(name) : null;
    }
}
