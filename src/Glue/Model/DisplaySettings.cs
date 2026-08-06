namespace FlatRedBall2.Glue.Model;

/// <summary>
/// Glue's project-level resolution and window setup. Phase 1 parses this and applies none of it;
/// translating it onto FRB2's own <c>DisplaySettings</c> is Phase 13.
/// </summary>
/// <remarks>
/// A <c>.gluj</c> also carries <c>ResolutionWidth</c>/<c>ResolutionHeight</c>/<c>In2D</c> at its
/// root, predating this type. FRB1 marks those as "eventually should get removed," so this block is
/// the authoritative one where the two disagree.
/// </remarks>
public class DisplaySettings
{
    /// <summary>
    /// Reproduces FRB1's <c>SetDefaults()</c>. Glue omits a member equal to its default, so these
    /// are exactly the values that never appear on disk — and reading them as <c>default(T)</c>
    /// gives a zero-sized window and the wrong dominant axis.
    /// </summary>
    public DisplaySettings()
    {
        Scale = 100;
        ScaleGum = 100;
        GenerateDisplayCode = true;
        DominantInternalCoordinates = 1; // Height
        TextureFilter = 0;               // Linear, per FRB1's SetDefaults
    }

    /// <summary>
    /// Whether Glue generates display setup at all. <c>false</c> means the game configures its own
    /// camera, so a loader should apply nothing.
    /// </summary>
    public bool GenerateDisplayCode { get; set; }

    /// <summary>The preset's name, or <c>Custom</c>.</summary>
    public string? Name { get; set; }

    /// <summary>Internal render width in pixels.</summary>
    public int ResolutionWidth { get; set; }

    /// <summary>Internal render height in pixels.</summary>
    public int ResolutionHeight { get; set; }

    /// <summary>Window width as a percentage of the internal resolution.</summary>
    public int Scale { get; set; }

    /// <summary>Gum canvas scale as a percentage.</summary>
    public int ScaleGum { get; set; }

    /// <summary>Whether the game runs full screen.</summary>
    public bool RunInFullScreen { get; set; }

    /// <summary>Whether the user may resize the window.</summary>
    public bool AllowWindowResizing { get; set; }

    /// <summary>Whether the game is treated as 2D.</summary>
    public bool Is2D { get; set; }

    /// <summary>Numerator of the locked aspect ratio, when one is enforced.</summary>
    public double AspectRatioWidth { get; set; }

    /// <summary>Denominator of the locked aspect ratio, when one is enforced.</summary>
    public double AspectRatioHeight { get; set; }

    /// <summary>How the view responds to a window size change.</summary>
    public int ResizeBehavior { get; set; }

    /// <summary>Which axis stays fixed when the aspect ratio changes.</summary>
    public int DominantInternalCoordinates { get; set; }

    /// <summary>How aspect ratio is constrained. FRB2 has no equivalent of Glue's ranged mode.</summary>
    public int AspectRatioBehavior { get; set; }

    /// <summary>Texture sampling mode.</summary>
    public int TextureFilter { get; set; }
}
