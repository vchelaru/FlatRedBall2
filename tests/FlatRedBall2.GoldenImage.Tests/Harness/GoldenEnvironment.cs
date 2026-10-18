using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace FlatRedBall2.GoldenImage;

/// <summary>
/// Where goldens live and whether golden scenes run at all. Everything is driven by environment
/// variables so the CI jobs, not the test code, decide the mode.
/// </summary>
public static class GoldenEnvironment
{
    /// <summary>Set to <c>1</c> by the CI job. Without it every golden scene skips.</summary>
    public const string EnableVariable = "FRB_GOLDEN_IMAGES";

    /// <summary>Set to <c>1</c> by the regenerate workflow to write goldens instead of comparing.</summary>
    public const string UpdateVariable = "FRB_GOLDEN_UPDATE";

    /// <summary>Where failure images are written. CI uploads this folder as an artifact.</summary>
    public const string ArtifactsVariable = "FRB_GOLDEN_ARTIFACTS";

    /// <summary>Why golden scenes should skip on this machine, or null when they should run.</summary>
    public static string? SkipReason
    {
        get
        {
            // Never create a device on macOS: SDL's Cocoa backend aborts the whole test host when
            // video init runs off the main thread (#1416). GraphicsDeviceFixture also refuses.
            if (OperatingSystem.IsMacOS())
                return "golden images are rendered on the CI llvmpipe job; macOS never creates a GL device (#1416)";

            if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
                return $"golden images are rendered on the CI llvmpipe job; set {EnableVariable}=1 to run them (local GPUs render differently)";

            return null;
        }
    }

    /// <summary>
    /// Update mode is for the CI regenerate workflow only: goldens must come from llvmpipe, and a
    /// local run would overwrite them with another GPU's output.
    /// </summary>
    public static bool UpdateGoldens
    {
        get
        {
            if (Environment.GetEnvironmentVariable(UpdateVariable) != "1")
                return false;

            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
                throw new InvalidOperationException(
                    $"{UpdateVariable}=1 is only honored on GitHub Actions. Goldens come from the CI llvmpipe " +
                    "output; run the 'Regenerate golden images' workflow instead.");

            return true;
        }
    }

    public static string GoldenDirectory =>
        typeof(GoldenEnvironment).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "GoldenDirectory").Value!;

    public static string ArtifactDirectory =>
        Environment.GetEnvironmentVariable(ArtifactsVariable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(AppContext.BaseDirectory, "golden-artifacts");

    public static GoldenOptions Options() =>
        new(GoldenDirectory, ArtifactDirectory, UpdateGoldens);
}

/// <summary>
/// Like <c>[Fact]</c>, but skipped (not passed) where golden scenes must not run: macOS, and any
/// machine that has not opted in with <see cref="GoldenEnvironment.EnableVariable"/>.
/// </summary>
public sealed class GoldenFactAttribute : FactAttribute
{
    public GoldenFactAttribute() => Skip = GoldenEnvironment.SkipReason;
}

/// <summary>The <see cref="TheoryAttribute"/> counterpart of <see cref="GoldenFactAttribute"/>.</summary>
public sealed class GoldenTheoryAttribute : TheoryAttribute
{
    public GoldenTheoryAttribute() => Skip = GoldenEnvironment.SkipReason;
}

public static class GoldenDevice
{
    /// <summary>
    /// The shared device. On a machine where golden scenes run, a missing device is a failure: a
    /// broken xvfb or Mesa install must not turn the whole job into a green no-op.
    /// </summary>
    public static Microsoft.Xna.Framework.Graphics.GraphicsDevice Require(FlatRedBall2.Tests.GraphicsDeviceFixture fixture)
    {
        if (!fixture.IsAvailable)
            throw new XunitException(
                "No GL graphics device, but golden scenes are enabled. On CI check the xvfb-run wrapper and " +
                "that libgl1-mesa-dri is installed with LIBGL_ALWAYS_SOFTWARE=1.");

        return fixture.GraphicsDevice!;
    }
}
