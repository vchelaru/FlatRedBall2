using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class ElementSystemTests
{
    [Fact]
    public void GetMultiplier_ExplicitAffinity_UsesExplicitValue()
    {
        var element = Element.Fire;
        var explicitMultiplier = 2.0f;
        var affinities = new List<ElementAffinity>
        {
            new() { Element = Element.Fire, Multiplier = explicitMultiplier }
        };

        var result = ElementSystem.GetMultiplier(element, affinities);

        result.ShouldBe(explicitMultiplier);
    }

    [Fact]
    public void GetMultiplier_NoElement_ReturnsNeutral()
    {
        var affinities = new List<ElementAffinity>
        {
            new() { Element = Element.Fire, Multiplier = 2.0f }
        };

        var result = ElementSystem.GetMultiplier(Element.None, affinities);

        result.ShouldBe(1.0f);
    }

    [Fact]
    public void IsStrongAgainst_FireBeatsIce_ReturnsTrue()
    {
        ElementSystem.IsStrongAgainst(Element.Fire, Element.Ice).ShouldBeTrue();
    }

    [Fact]
    public void IsStrongAgainst_IceDoesNotBeatFire_ReturnsFalse()
    {
        ElementSystem.IsStrongAgainst(Element.Ice, Element.Fire).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Element.Steam, Element.Glitch, true)]
    [InlineData(Element.Glitch, Element.Aether, true)]
    [InlineData(Element.Aether, Element.Steam, true)]
    [InlineData(Element.Ice, Element.Lightning, true)]
    [InlineData(Element.Lightning, Element.Fire, true)]
    public void IsStrongAgainst_TriangleRelationships(Element attacker, Element defender, bool expected)
    {
        ElementSystem.IsStrongAgainst(attacker, defender).ShouldBe(expected);
    }

    [Fact]
    public void GetMultiplier_SameElement_Returns050()
    {
        var affinities = new List<ElementAffinity>
        {
            new() { Element = Element.Fire, Multiplier = 0.5f }
        };

        // Explicit affinity for same element = 0.5
        ElementSystem.GetMultiplier(Element.Fire, affinities).ShouldBe(0.5f);
    }
}
