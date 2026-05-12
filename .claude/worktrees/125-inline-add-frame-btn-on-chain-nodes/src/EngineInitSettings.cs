namespace FlatRedBall2;

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
}
