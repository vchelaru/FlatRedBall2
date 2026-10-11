# Export an Animation as a GIF

The AnimationEditor can save the selected animations as an animated GIF, handy for sharing work in progress in chat or a pull request.

## Export

1. Select one or more animations in the animation list.
2. Choose **File** > **Export** > **Animated GIF...**.
3. Pick a frame rate, background and size, then click **Export**.
4. Choose where to save the file.

The GIF looks like the preview: frame offsets are applied, and the **Interpolate** setting of each animation is respected. Collision shapes, guides and the origin are not drawn.

## Several Animations

Selecting multiple animations stacks them in one GIF, with the animation selected last on top, the same as the preview.

The GIF is as long as the longest selected animation. Shorter animations that loop repeat until the GIF ends, and shorter animations that do not loop hold their last frame.

## Options

### Frame Rate

The GIF format stores each frame's delay in whole hundredths of a second, and browsers play a frame with a delay of 10 milliseconds or less at 100 milliseconds. That makes 50 fps (20 milliseconds per frame) the fastest usable rate, so 60 fps is not offered. The dialog offers 50, 30, 25 and 10 fps.

Frames that look the same as the one before them are merged into a single longer frame, so an animation without **Interpolate** produces about one GIF frame per animation frame regardless of the frame rate.

### Background

**Transparent** makes the background see-through. GIF transparency is on or off for each pixel, with no partial blending, so soft or antialiased edges show a jagged fringe. Pixel art without partial transparency is not affected.

**Solid color** fills the background with a color. The color starts as the editor's canvas background.

### Size

The size option scales the GIF up by a whole number, from 1x to 8x, keeping pixels sharp. Pixel art is often too small to read when shared at its original size.

## Limits

A GIF holds at most 256 colors. Pixel art that uses 256 or fewer colors across the whole animation is saved exactly, and richer art is reduced to a shared palette of 256 colors.
