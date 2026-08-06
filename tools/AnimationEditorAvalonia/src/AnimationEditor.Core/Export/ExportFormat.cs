using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Descriptor for a single export target: dialog labels plus the pure converter that turns an
/// <see cref="AnimationChainListSave"/> into an <see cref="ExportResult"/>.
/// </summary>
public sealed class ExportFormat
{
    public ExportFormat(
        string dialogTitle,
        string extension,
        string filter,
        Func<AnimationChainListSave, Func<string, (int Width, int Height)?>, ExportResult> export)
    {
        DialogTitle = dialogTitle;
        Extension = extension;
        Filter = filter;
        Export = export;
    }

    /// <summary>File-dialog title (e.g. "Export to PixiJS").</summary>
    public string DialogTitle { get; }

    /// <summary>Default file extension without the leading dot (e.g. "json", "tres").</summary>
    public string Extension { get; }

    /// <summary>File-dialog filter string (e.g. "PixiJS Spritesheet (*.json)").</summary>
    public string Filter { get; }

    /// <summary>Pure converter: acls + texture-size resolver → <see cref="ExportResult"/>.</summary>
    public Func<AnimationChainListSave, Func<string, (int Width, int Height)?>, ExportResult> Export { get; }
}

/// <summary>Built-in export format descriptors for the Animation Editor.</summary>
public static class ExportFormats
{
    public static ExportFormat PixiJs { get; } = new(
        "Export to PixiJS",
        "json",
        "PixiJS Spritesheet (*.json)",
        PixiJsSpriteSheetExporter.Export);

    public static ExportFormat Godot { get; } = new(
        "Export to Godot SpriteFrames",
        "tres",
        "Godot SpriteFrames (*.tres)",
        GodotSpriteFramesExporter.Export);
}
