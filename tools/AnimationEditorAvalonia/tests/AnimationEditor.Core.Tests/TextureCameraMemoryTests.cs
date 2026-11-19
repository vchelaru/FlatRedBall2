using AnimationEditor.Core.Models;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class TextureCameraMemoryTests
{
    private static readonly TextureCamera A = new(1, 2, 3);
    private static readonly TextureCamera B = new(4, 5, 6);

    [Fact]
    public void Take_WithNothingRemembered_ReturnsNull()
    {
        new TextureCameraMemory().Take("a.png", out _).ShouldBeNull();
    }

    [Fact]
    public void Take_ReturnsTheRememberedCameraEveryTime()
    {
        var memory = new TextureCameraMemory();
        memory.Remember("a.png", A);

        memory.Take("a.png", out bool fromSeed).ShouldBe(A);
        fromSeed.ShouldBeFalse();
        memory.Take("a.png", out _).ShouldBe(A);
        memory.Take("b.png", out _).ShouldBeNull();
    }

    [Fact]
    public void Seed_AppliesOnceToTheFirstTexture()
    {
        var memory = new TextureCameraMemory();
        memory.Seed(A);

        memory.Take("a.png", out bool fromSeed).ShouldBe(A);
        fromSeed.ShouldBeTrue();
        memory.Take("b.png", out _).ShouldBeNull();
    }

    [Fact]
    public void Seed_IsIgnoredOnceTheTabHasBeenShown()
    {
        var memory = new TextureCameraMemory();
        memory.Remember("a.png", A);

        memory.Seed(B);

        memory.Take("c.png", out _).ShouldBeNull();
    }

    [Fact]
    public void Seed_IsIgnoredOnceAlreadySeeded()
    {
        var memory = new TextureCameraMemory();
        memory.Seed(A);
        memory.Take("a.png", out _);

        memory.Seed(B);

        memory.Take("b.png", out _).ShouldBeNull();
    }
}
