using System.Collections.Generic;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Format-agnostic result of an animation-chain export: serialized text plus any non-fatal
/// warnings and the textures the app layer should copy alongside the output file.
/// </summary>
public sealed class ExportResult
{
    public ExportResult(string text, IReadOnlyList<string> warnings, IReadOnlyList<string> referencedTextures)
    {
        Text = text;
        Warnings = warnings;
        ReferencedTextures = referencedTextures;
    }

    /// <summary>Serialized export payload (JSON, .tres text, etc.).</summary>
    public string Text { get; }

    /// <summary>Human-readable warnings (dropped fields, missing PNGs, fidelity gaps).</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Distinct texture names referenced by the exported frames, in first-seen order. The app
    /// layer copies these alongside the export when writing to a different directory.
    /// </summary>
    public IReadOnlyList<string> ReferencedTextures { get; }
}
