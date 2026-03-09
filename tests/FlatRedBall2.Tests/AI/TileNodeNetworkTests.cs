using System.Collections.Generic;
using FlatRedBall2.AI;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.AI;

public class TileNodeNetworkTests
{
    // ── AddAndLinkNode ───────────────────────────────────────────────────────

    [Fact]
    public void AddAndLinkNode_AdjacentNodes_AreLinked()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 3, DirectionalType.Four);

        var a = network.AddAndLinkNode(0, 0);
        var b = network.AddAndLinkNode(1, 0);

        a.Neighbors.ShouldContain(b);
        b.Neighbors.ShouldContain(a);
    }

    [Fact]
    public void AddAndLinkNode_DiagonalInFourWay_NotLinked()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 3, DirectionalType.Four);

        var a = network.AddAndLinkNode(0, 0);
        var b = network.AddAndLinkNode(1, 1);

        a.Neighbors.ShouldNotContain(b);
    }

    [Fact]
    public void AddAndLinkNode_DiagonalInEightWay_IsLinked()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 3, DirectionalType.Eight);

        var a = network.AddAndLinkNode(0, 0);
        var b = network.AddAndLinkNode(1, 1);

        a.Neighbors.ShouldContain(b);
    }

    [Fact]
    public void AddAndLinkNode_DuplicateCall_ReturnsSameNode()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 3, DirectionalType.Four);

        var first  = network.AddAndLinkNode(1, 1);
        var second = network.AddAndLinkNode(1, 1);

        second.ShouldBeSameAs(first);
    }

    // ── RemoveAt ─────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveAt_ExistingNode_UnlinksNeighbors()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 1, DirectionalType.Four);
        network.FillCompletely();

        var left  = network.NodeAt(0, 0)!;
        var right = network.NodeAt(2, 0)!;

        network.RemoveAt(1, 0);

        left.Neighbors.ShouldBeEmpty();
        right.Neighbors.ShouldBeEmpty();
        network.NodeAt(1, 0).ShouldBeNull();
    }

    // ── EliminateCutCorners ───────────────────────────────────────────────────

    [Fact]
    public void EliminateCutCorners_DiagonalThroughMissingCardinal_RemovesLink()
    {
        // Grid: (0,0) and (1,1) are present, but (0,1) is missing.
        // The diagonal 0,0 → 1,1 crosses through the missing (0,1) corner.
        var network = new TileNodeNetwork(0f, 0f, 16f, 2, 2, DirectionalType.Eight);
        var a = network.AddAndLinkNode(0, 0);
        var b = network.AddAndLinkNode(1, 1);
        // (1,0) present but (0,1) absent — diagonal should be cut
        network.AddAndLinkNode(1, 0);

        network.EliminateCutCorners();

        a.Neighbors.ShouldNotContain(b);
    }

    // ── GetPath ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetPath_StraightLine_ReturnsCorrectWaypoints()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 4, 1, DirectionalType.Four);
        network.FillCompletely();

        var start = network.NodeAt(0, 0)!;
        var end   = network.NodeAt(3, 0)!;

        var path = network.GetPath(start, end);

        // Start is excluded; end is included — expect positions at x=16,32,48 (indices 1,2,3)
        path.Count.ShouldBe(3);
        path[0].ShouldBe(new Vector2(16f, 0f));
        path[2].ShouldBe(new Vector2(48f, 0f));
    }

    [Fact]
    public void GetPath_StartEqualsEnd_ReturnsEmptyList()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 3, DirectionalType.Four);
        network.FillCompletely();
        var node = network.NodeAt(1, 1)!;

        var path = network.GetPath(node, node);

        path.ShouldBeEmpty();
    }

    [Fact]
    public void GetPath_NoPathExists_ReturnsFalse()
    {
        // Two isolated nodes with no edges
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 1, DirectionalType.Four);
        var start = network.AddAndLinkNode(0, 0);
        var end   = network.AddAndLinkNode(2, 0); // gap at (1,0) — never added

        var found = network.GetPath(start, end, new List<Vector2>());

        found.ShouldBeFalse();
    }

    // ── GetClosestNode ────────────────────────────────────────────────────────

    [Fact]
    public void GetClosestNode_WorldPositionBetweenNodes_ReturnsNearest()
    {
        var network = new TileNodeNetwork(0f, 0f, 16f, 3, 1, DirectionalType.Four);
        network.AddAndLinkNode(0, 0); // world (0,0)
        network.AddAndLinkNode(2, 0); // world (32,0)

        // Position at (10,0) — closer to (0,0)
        var closest = network.GetClosestNode(10f, 0f);

        closest!.Position.ShouldBe(new Vector2(0f, 0f));
    }
}
