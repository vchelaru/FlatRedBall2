using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NamedTextureResolverTests
{
    [Fact]
    public async Task ResolveSizesAsync_IncludesEveryNameTheResolverFinds()
    {
        var sizes = await NamedTextureResolver.ResolveSizesAsync(
            new[] { "player.png", "enemy.png" },
            name => Task.FromResult<(int Width, int Height)?>(name == "player.png" ? (32, 32) : (16, 16)));

        Assert.Equal((32, 32), sizes["player.png"]);
        Assert.Equal((16, 16), sizes["enemy.png"]);
    }

    [Fact]
    public async Task ResolveSizesAsync_SkipsNamesTheResolverCannotFind()
    {
        // Models a name-based lookup that can't reach a texture -- e.g. the cross-folder-texture
        // problem noted as separate/out-of-scope in issue #763 -- without throwing or aborting
        // the rest of the load.
        var sizes = await NamedTextureResolver.ResolveSizesAsync(
            new[] { "player.png", "missing.png" },
            name => Task.FromResult<(int Width, int Height)?>(name == "player.png" ? (32, 32) : null));

        Assert.True(sizes.ContainsKey("player.png"));
        Assert.False(sizes.ContainsKey("missing.png"));
    }

    [Fact]
    public async Task ResolveSizesAsync_KeysAreCaseInsensitive()
    {
        var sizes = await NamedTextureResolver.ResolveSizesAsync(
            new[] { "Player.png" },
            _ => Task.FromResult<(int Width, int Height)?>((32, 32)));

        Assert.True(sizes.ContainsKey("player.png"));
    }
}
