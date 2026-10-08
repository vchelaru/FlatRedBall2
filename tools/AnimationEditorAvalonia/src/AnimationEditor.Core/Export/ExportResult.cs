using System.Collections.Generic;

namespace AnimationEditor.Core.Export;

/// <summary>Result of an export: the file text plus any non-fatal warnings to surface.</summary>
public sealed class ExportResult
{
    public ExportResult(string text, IReadOnlyList<string> warnings, IReadOnlyList<string> referencedTextures)
    {
        Text = text;
        Warnings = warnings;
        ReferencedTextures = referencedTextures;
    }

    /// <summary>The serialized export file contents.</summary>
    public string Text { get; }

    /// <summary>Human-readable warnings about data the target format could not carry.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Distinct texture names referenced by the exported frames, in first-seen order. The app
    /// layer copies these alongside the export when writing to a different directory, since every
    /// supported format references textures relative to the exported file.
    /// </summary>
    public IReadOnlyList<string> ReferencedTextures { get; }
}
