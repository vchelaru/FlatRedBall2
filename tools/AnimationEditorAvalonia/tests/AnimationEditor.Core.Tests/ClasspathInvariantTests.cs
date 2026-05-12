using System.Reflection;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Guards the invariant that MonoGame.Framework is absent from the Core.Tests classpath.
///
/// The live AE app ships without MonoGame (FRB2's PackageReference carries
/// PrivateAssets=All), so any FRB2 API path that JIT-resolves a
/// Microsoft.Xna.Framework.* symbol throws FileNotFoundException in production.
/// This test proves the test classpath matches that constraint: a failure here
/// means a FRB2 API called by the AE has a hidden MonoGame JIT-dependency that
/// must be fixed before it ships.
/// </summary>
public class ClasspathInvariantTests
{
    [Fact]
    public void MonoGameFramework_IsNotPresentInClasspath()
    {
        Assert.Throws<FileNotFoundException>(() => Assembly.Load("MonoGame.Framework"));
    }
}
