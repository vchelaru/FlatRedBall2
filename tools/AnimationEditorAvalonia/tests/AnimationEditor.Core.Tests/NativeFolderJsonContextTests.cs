using System.Text.Json;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NativeFolderJsonContextTests
{
    [Fact]
    public void Deserialize_StringArray_ParsesNames()
    {
        var json = "[\"player.achx\",\"player.png\"]";

        var names = JsonSerializer.Deserialize(json, NativeFolderJsonContext.Default.StringArray);

        Assert.Equal(new[] { "player.achx", "player.png" }, names);
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmpty()
    {
        var names = JsonSerializer.Deserialize("[]", NativeFolderJsonContext.Default.StringArray);

        Assert.Empty(names!);
    }

    [Fact]
    public void Deserialize_MatchesReflectionBasedResult()
    {
        // Parity check: the source-generated path must produce the same result the
        // reflection-based JsonSerializer.Deserialize<string[]>(json) call did before this fix --
        // desktop tests have reflection enabled and never caught the WASM-only crash (the same
        // gap PixiJsJsonContext's own tests document), so this guards against the
        // source-generated path silently diverging from what it replaces.
        var json = "[\"a.png\",\"b Sub\\\\Name.achx\"]";

        var sourceGenerated = JsonSerializer.Deserialize(json, NativeFolderJsonContext.Default.StringArray);
        var reflectionBased = JsonSerializer.Deserialize<string[]>(json);

        Assert.Equal(reflectionBased, sourceGenerated);
    }
}
