using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.Export;

/// <summary>
/// An export target: the pure converter plus what the save dialog needs. Pass one to
/// <c>IAppCommands.ExportAsync</c>; adding a format means adding an instance here, not a new command.
/// </summary>
public sealed class ExportFormat
{
    public ExportFormat(
        string dialogTitle,
        string extension,
        string fileTypeDescription,
        Func<AnimationChainListSave, Func<string, (int Width, int Height)?>, ExportResult> export)
    {
        DialogTitle = dialogTitle;
        Extension = extension;
        FileTypeDescription = fileTypeDescription;
        Export = export;
    }

    public string DialogTitle { get; }

    /// <summary>File extension without the dot, e.g. <c>json</c>.</summary>
    public string Extension { get; }

    public string FileTypeDescription { get; }

    /// <summary>Converts a project to file text; the second argument resolves a texture name to its pixel size.</summary>
    public Func<AnimationChainListSave, Func<string, (int Width, int Height)?>, ExportResult> Export { get; }

    public static ExportFormat PixiJs { get; } = new(
        "Export to PixiJS", "json", "PixiJS Spritesheet (*.json)", PixiJsSpriteSheetExporter.Export);

    public static ExportFormat Godot { get; } = new(
        "Export to Godot", "tres", "Godot SpriteFrames (*.tres)", GodotSpriteFramesExporter.Export);
}
