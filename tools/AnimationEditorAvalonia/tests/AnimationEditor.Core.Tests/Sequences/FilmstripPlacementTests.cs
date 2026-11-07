using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class FilmstripPlacementTests
{
    [Fact]
    public void Place_SpriteInsideTheCell_CopiesTheWholeThumbnail()
    {
        var placement = FilmstripPlacement.Place(originX: 0, originY: 0, scale: 1, cellLeft: 10, cellRight: 60, filmTop: 20, filmBottom: 64,
            spriteLeft: 10, spriteTop: 20, spriteWidth: 44, spriteHeight: 44);

        placement.ShouldBe(new CellPlacement(10, 20, 60, 64, true, 0, 0, 10, 20, 54, 64));
    }

    [Fact]
    public void Place_CellNarrowerThanTheSprite_CopiesTheWindowUnderIt()
    {
        // The sprite starts 6 px left of the cell and the cell is 10 px wide.
        var placement = FilmstripPlacement.Place(0, 0, 1, cellLeft: 100, cellRight: 110, filmTop: 0, filmBottom: 44,
            spriteLeft: 94, spriteTop: 0, spriteWidth: 44, spriteHeight: 44);

        placement.HasSprite.ShouldBeTrue();
        placement.SourceX.ShouldBe(6);
        (placement.SpriteRight - placement.SpriteLeft).ShouldBe(10);
    }

    [Fact]
    public void Place_SpriteOutsideTheCell_HasNoSprite()
    {
        var placement = FilmstripPlacement.Place(0, 0, 1, cellLeft: 0, cellRight: 10, filmTop: 0, filmBottom: 44,
            spriteLeft: 10, spriteTop: 0, spriteWidth: 44, spriteHeight: 44);

        placement.HasSprite.ShouldBeFalse();
    }

    [Fact]
    public void Place_FractionalScale_SnapsNeighbouringCellsToTheSameEdge()
    {
        // At 1.5x the edge at 7 is pixel 10.5; both cells must round it the same way, so there is no gap or overlap.
        var left = FilmstripPlacement.Place(0.25, 0, 1.5, cellLeft: 0, cellRight: 7, filmTop: 0, filmBottom: 44, 0, 0, 66, 66);
        var right = FilmstripPlacement.Place(0.25, 0, 1.5, cellLeft: 7, cellRight: 14, filmTop: 0, filmBottom: 44, 0, 0, 66, 66);

        left.CellRight.ShouldBe(right.CellLeft);
    }

    [Fact]
    public void Place_FractionalScale_CopiesWholePixelsOnly()
    {
        var placement = FilmstripPlacement.Place(0, 0, 1.5, cellLeft: 0, cellRight: 20, filmTop: 0, filmBottom: 44,
            spriteLeft: 3, spriteTop: 2, spriteWidth: 48, spriteHeight: 48);

        // The sprite's left lands on pixel 4.5 -> 5; the copy is the same size as the source slice.
        placement.SpriteLeft.ShouldBe(5);
        (placement.SpriteRight - placement.SpriteLeft).ShouldBe(25);   // the cell ends at pixel 30
    }
}
