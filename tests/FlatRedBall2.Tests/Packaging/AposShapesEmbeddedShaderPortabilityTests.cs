using System;
using System.Reflection;
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
        // Since Apos.Shapes 0.10.0 the 16 shader techniques ship as one chunked, compressed pack
        // ("apos-shapes.knifx.pack"); its internal ShaderArchive unpacks one .knifx per technique.
        // The dx11 pack holds no GLSL.
        const string Resource = "Apos.Shapes.apos-shapes.knifx.pack";
        var archiveType = assembly.GetType("Apos.Shapes.ShaderArchive");
        var forMethod = archiveType?.GetMethod("For", BindingFlags.Public | BindingFlags.Static);
        var namesProperty = archiveType?.GetProperty("Names", BindingFlags.Public | BindingFlags.Instance);
        var readMethod = archiveType?.GetMethod("Read", BindingFlags.Public | BindingFlags.Instance);
        if (forMethod == null || namesProperty == null || readMethod == null || assembly.GetManifestResourceStream(Resource) == null)
            throw new InvalidOperationException(
                "Apos.Shapes.KNI.dll no longer has the internal ShaderArchive over the embedded resource '" + Resource + "' -- " +
                "update this test to match the current package.");

        var archive = forMethod.Invoke(null, new object[] { Resource })!;
        var sb = new StringBuilder();
        foreach (var name in (System.Collections.Generic.IEnumerable<int>)namesProperty.GetValue(archive)!)
        {
            var knifx = (byte[])readMethod.Invoke(archive, new object[] { name })!;
            sb.Append(Encoding.Latin1.GetString(knifx));
        }
        var glsl = sb.ToString();
        glsl.Contains("gl_FragColor").ShouldBeTrue("the unpacked shader pack should hold GLSL, or this test checks nothing");

        BareTrunc.IsMatch(glsl).ShouldBeFalse(
            "The embedded OpenGL shader calls trunc(), a GLSL 1.30/ES 3.00 builtin absent from the " +
            "legacy dialect MonoGame's GL runtime requires. It compiles on lenient desktop drivers " +
            "but fails as an undeclared identifier on macOS DesktopGL (issue #853). Bump " +
            "AposShapesVersion in Directory.Packages.props to a release built after ShadowDusk's " +
            "trunc() lowering fix.");
    }
}
