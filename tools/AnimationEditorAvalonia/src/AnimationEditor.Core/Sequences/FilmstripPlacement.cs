using System;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// A filmstrip cell in whole device pixels. <see cref="Cell"/> is the cell itself; the sprite part is the slice of the
/// sprite thumbnail inside it: copy <see cref="SourceX"/>, <see cref="SourceY"/> (pixels into the thumbnail) to the
/// rectangle <see cref="SpriteLeft"/>..<see cref="SpriteRight"/> by <see cref="SpriteTop"/>..<see cref="SpriteBottom"/>, same size, no scaling.
/// </summary>
public readonly record struct CellPlacement(
    int CellLeft, int CellTop, int CellRight, int CellBottom,
    bool HasSprite, int SourceX, int SourceY, int SpriteLeft, int SpriteTop, int SpriteRight, int SpriteBottom);

/// <summary>
/// Snaps a filmstrip cell and its sprite to the device pixel grid, so the thumbnail (cut at the display's pixel height) is
/// copied one to one instead of being resampled at a fractional offset. Positions are layout units; the origin and
/// scale are the render target's (render scaling), so a cell edge shared by two cells lands on the same pixel for both.
/// </summary>
public static class FilmstripPlacement
{
    public static CellPlacement Place(
        double originX, double originY, double scale,
        double cellLeft, double cellRight, double filmTop, double filmBottom,
        double spriteLeft, double spriteTop, int spriteWidth, int spriteHeight)
    {
        int left = Snap(originX + cellLeft * scale), right = Snap(originX + cellRight * scale);
        int top = Snap(originY + filmTop * scale), bottom = Snap(originY + filmBottom * scale);
        if (spriteWidth <= 0 || spriteHeight <= 0)
            return new CellPlacement(left, top, right, bottom, false, 0, 0, 0, 0, 0, 0);

        int spriteX = Snap(originX + spriteLeft * scale), spriteY = Snap(originY + spriteTop * scale);
        int shownLeft = Math.Max(left, spriteX), shownTop = Math.Max(top, spriteY);
        int shownRight = Math.Min(right, spriteX + spriteWidth), shownBottom = Math.Min(bottom, spriteY + spriteHeight);
        if (shownRight <= shownLeft || shownBottom <= shownTop)
            return new CellPlacement(left, top, right, bottom, false, 0, 0, 0, 0, 0, 0);
        return new CellPlacement(left, top, right, bottom, true, shownLeft - spriteX, shownTop - spriteY, shownLeft, shownTop, shownRight, shownBottom);
    }

    private static int Snap(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
