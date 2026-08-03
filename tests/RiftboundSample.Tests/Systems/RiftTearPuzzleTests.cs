using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class RiftTearPuzzleTests
{
    [Fact]
    public void Generate_Difficulty1_IsNotSolved()
    {
        int difficulty = 1;
        var puzzle = RiftTearPuzzle.Generate(difficulty, new Random(42));

        puzzle.IsSolved.ShouldBeFalse();
    }

    [Fact]
    public void Generate_Difficulty3_IsNotSolved()
    {
        int difficulty = 3;
        var puzzle = RiftTearPuzzle.Generate(difficulty, new Random(99));

        puzzle.IsSolved.ShouldBeFalse();
    }

    [Fact]
    public void IsSolved_MatchingGridAndTarget_ReturnsTrue()
    {
        var puzzle = new RiftTearPuzzle();
        // Both Grid and Target default to Element.None (all zeroes), which matches
        // Set them to the same values explicitly
        puzzle.Grid[0, 0] = Element.Fire;
        puzzle.Grid[1, 1] = Element.Ice;
        puzzle.Target[0, 0] = Element.Fire;
        puzzle.Target[1, 1] = Element.Ice;

        puzzle.IsSolved.ShouldBeTrue();
    }

    [Fact]
    public void RotateRow_Right_ShiftsElements()
    {
        var puzzle = new RiftTearPuzzle();
        puzzle.Grid[0, 0] = Element.Fire;
        puzzle.Grid[0, 1] = Element.Ice;
        puzzle.Grid[0, 2] = Element.Steam;

        puzzle.RotateRow(0, right: true);

        // Right shift: [Fire, Ice, Steam] -> [Steam, Fire, Ice]
        puzzle.Grid[0, 0].ShouldBe(Element.Steam);
        puzzle.Grid[0, 1].ShouldBe(Element.Fire);
        puzzle.Grid[0, 2].ShouldBe(Element.Ice);
    }

    [Fact]
    public void RotateRow_Left_ShiftsElements()
    {
        var puzzle = new RiftTearPuzzle();
        puzzle.Grid[0, 0] = Element.Fire;
        puzzle.Grid[0, 1] = Element.Ice;
        puzzle.Grid[0, 2] = Element.Steam;

        puzzle.RotateRow(0, right: false);

        // Left shift: [Fire, Ice, Steam] -> [Ice, Steam, Fire]
        puzzle.Grid[0, 0].ShouldBe(Element.Ice);
        puzzle.Grid[0, 1].ShouldBe(Element.Steam);
        puzzle.Grid[0, 2].ShouldBe(Element.Fire);
    }

    [Fact]
    public void RotateColumn_Down_ShiftsElements()
    {
        var puzzle = new RiftTearPuzzle();
        puzzle.Grid[0, 0] = Element.Fire;
        puzzle.Grid[1, 0] = Element.Ice;
        puzzle.Grid[2, 0] = Element.Steam;

        puzzle.RotateColumn(0, down: true);

        // Down shift: [Fire, Ice, Steam] -> [Steam, Fire, Ice]
        puzzle.Grid[0, 0].ShouldBe(Element.Steam);
        puzzle.Grid[1, 0].ShouldBe(Element.Fire);
        puzzle.Grid[2, 0].ShouldBe(Element.Ice);
    }

    [Fact]
    public void RotateRow_RightThenLeft_RestoresOriginal()
    {
        var puzzle = new RiftTearPuzzle();
        puzzle.Grid[1, 0] = Element.Fire;
        puzzle.Grid[1, 1] = Element.Lightning;
        puzzle.Grid[1, 2] = Element.Aether;

        puzzle.RotateRow(1, right: true);
        puzzle.RotateRow(1, right: false);

        puzzle.Grid[1, 0].ShouldBe(Element.Fire);
        puzzle.Grid[1, 1].ShouldBe(Element.Lightning);
        puzzle.Grid[1, 2].ShouldBe(Element.Aether);
    }
}
