using System.IO;
using System.Reflection;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Issue #1582. KNI 4.3.9001 reads effect format v12 and prints "This effect is for an older version
// of KNI and needs to be rebuilt." at startup for anything older. The precompiled AddColor shader
// must therefore be a KNIFX v12 container (ShadowDusk --target-runtime kni-knifx-v12), not the
// legacy MGFX v10. Constructing the Effect needs a GraphicsDevice, so this pins the embedded bytes.
public class AddColorEffectResourceTests
{
    private const string ResourceName = "FlatRedBall2.Rendering.Shaders.AddColor.knifx";

    [Fact]
    public void AddColorResource_IsKnifxVersion12()
    {
        using var stream = typeof(FlatRedBallService).Assembly.GetManifestResourceStream(ResourceName);
        stream.ShouldNotBeNull("The AddColor shader must be embedded as a .knifx resource named " + ResourceName);

        var header = new byte[8];
        stream.ReadExactly(header);

        System.Text.Encoding.ASCII.GetString(header, 0, 4).ShouldBe("KNIF");
        System.BitConverter.ToInt32(header, 4).ShouldBe(12);
    }
}
