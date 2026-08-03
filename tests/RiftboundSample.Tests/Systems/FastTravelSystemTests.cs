using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class FastTravelSystemTests
{
    [Fact]
    public void HasVisited_AfterMarkVisited_ReturnsTrue()
    {
        var system = new FastTravelSystem();
        string mapId = "brasshollow";

        system.MarkVisited(mapId);

        system.HasVisited(mapId).ShouldBeTrue();
    }

    [Fact]
    public void HasVisited_NotVisited_ReturnsFalse()
    {
        var system = new FastTravelSystem();
        string mapId = "rustfields";

        system.HasVisited(mapId).ShouldBeFalse();
    }

    [Fact]
    public void LoadFrom_RestoresVisitedMaps()
    {
        var system = new FastTravelSystem();
        var savedMaps = new List<string> { "brasshollow", "rustfields" };

        system.LoadFrom(savedMaps);

        system.HasVisited("brasshollow").ShouldBeTrue();
        system.HasVisited("rustfields").ShouldBeTrue();
        system.HasVisited("cogspire_academy").ShouldBeFalse();
    }

    [Fact]
    public void MarkVisited_Duplicate_DoesNotAddTwice()
    {
        var system = new FastTravelSystem();
        string mapId = "brasshollow";

        system.MarkVisited(mapId);
        system.MarkVisited(mapId);

        system.VisitedMaps.Count.ShouldBe(1);
    }
}
