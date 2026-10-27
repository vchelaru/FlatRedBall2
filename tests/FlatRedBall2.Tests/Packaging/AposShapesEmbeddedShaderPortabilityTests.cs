using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Apos.Shapes;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

// Issue #853. Apos.Shapes' OpenGL shader is built by ShadowDusk's DXC->SPIRV-Cross pipeline, which
// (before ShadowDusk's "Rule 15" trunc() lowering, tracked as Apos.Shapes #34) emitted a bare
// trunc() call for HLSL's truncating %/fmod operator. trunc() is a GLSL 1.30 / GLSL ES 3.00
// builtin, absent from the legacy, versionless GLSL dialect MonoGame's OpenGL runtime requires --
// it silently compiled on lenient desktop drivers but failed as an undeclared identifier on macOS
// DesktopGL (Apple Silicon), crashing any Screen that drew a shape on its first frame.
//
// Apos.Shapes 0.7.8 (pinned when #853 was filed) was published ~10 hours before the ShadowDusk fix
// landed; 0.7.9+ ships it (confirmed by extracting and diffing the embedded shader resource of both
// versions' published NuGet packages). This guards the pin against ever regressing back to a build
// with the bug, independent of which exact version is pinned.
public class AposShapesEmbeddedShaderPortabilityTests
{
    private static readonly Regex BareTrunc = new(@"\btrunc\s*\(", RegexOptions.Compiled);

    [Fact]
    public void EmbeddedOpenGLShader_DoesNotCallBareTrunc()
    {
        var assembly = typeof(ShapeBatch).Assembly;
        // Gzipped since Apos.Shapes 0.9.0; one resource per shader technique ("apos-shapes.N.knifx.gz")
        // since 0.9.3. The dx11 .mgfx.gz holds no GLSL.
        var names = Array.FindAll(assembly.GetManifestResourceNames(),
            n => n.StartsWith("Apos.Shapes.apos-shapes.", StringComparison.Ordinal) && n.EndsWith(".knifx.gz", StringComparison.Ordinal));
        if (names.Length == 0)
            throw new InvalidOperationException(
                "Apos.Shapes.KNI.dll no longer embeds resources named 'apos-shapes.*.knifx.gz' -- " +
                "update this test's resource names to match the current package.");

        var sb = new StringBuilder();
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.Latin1);
            sb.Append(reader.ReadToEnd());
        }
        var glsl = sb.ToString();

        BareTrunc.IsMatch(glsl).ShouldBeFalse(
            "The embedded OpenGL shader calls trunc(), a GLSL 1.30/ES 3.00 builtin absent from the " +
            "legacy dialect MonoGame's GL runtime requires. It compiles on lenient desktop drivers " +
            "but fails as an undeclared identifier on macOS DesktopGL (issue #853). Bump " +
            "AposShapesVersion in Directory.Packages.props to a release built after ShadowDusk's " +
            "trunc() lowering fix.");
    }
}
