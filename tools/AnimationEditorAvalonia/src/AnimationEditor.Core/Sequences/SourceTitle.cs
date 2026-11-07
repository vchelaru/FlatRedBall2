namespace AnimationEditor.Core.Sequences;

/// <summary>How a library source is titled: its file name, since the folder it sits in is rarely what tells sources apart.</summary>
public static class SourceTitle
{
    /// <summary>
    /// The part of <paramref name="source"/> after its last <c>/</c> or <c>\</c>. A source with no file name part, such as
    /// <c>chars/</c> or <c>..</c>, is returned as written.
    /// </summary>
    public static string Format(string source)
    {
        string name = source[(source.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];
        return name.Length == 0 || name == ".." || name == "." ? source : name;
    }
}
