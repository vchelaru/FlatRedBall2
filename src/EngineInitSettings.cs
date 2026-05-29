using System;
using System.Collections.Generic;

namespace FlatRedBall2;

/// <summary>
/// Controls how startup validation reacts when configured Gum font files are missing.
/// </summary>
public enum MissingGumFontFileBehavior
{
    /// <summary>Write a warning and continue startup.</summary>
    Warn,
    /// <summary>Throw and fail startup when any configured file is missing.</summary>
    Throw
}

/// <summary>
/// Startup configuration passed to <see cref="FlatRedBallService.Initialize"/>.
/// All properties are read once at initialization time — changes after that call have no effect.
/// </summary>
public class EngineInitSettings
{
    /// <summary>
    /// Path to a Gum project file (.gumx) to load at startup.
    /// When set, Gum initializes in project mode and loads UI definitions from the file.
    /// When null, Gum initializes in code-only mode using default V3 visuals.
    /// </summary>
    public string? GumProjectFile { get; init; }

    /// <summary>
    /// Optional Gum font files to validate at startup (for example entries used by
    /// Text <c>CustomFontFile</c>). Relative paths are interpreted relative to the
    /// directory containing <see cref="GumProjectFile"/>.
    /// </summary>
    public IReadOnlyList<string> GumFontFilesToValidate { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Controls whether missing entries in <see cref="GumFontFilesToValidate"/> throw
    /// during startup or only produce a warning.
    /// </summary>
    public MissingGumFontFileBehavior MissingGumFontFileBehavior { get; init; } = MissingGumFontFileBehavior.Warn;
}
