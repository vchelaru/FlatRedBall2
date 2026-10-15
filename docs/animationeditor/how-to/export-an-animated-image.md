# Export an Animation as a GIF or WebP

The AnimationEditor can save the selected animations as an animated GIF or an animated WebP, handy for sharing work in progress in chat or a pull request.

## Export

1. Select one or more animations in the animation list.
2. Choose **File** > **Export** > **Animated GIF...** or **Animated WebP...**.
3. Pick a frame rate, background and size, then click **Export**.
4. Choose where to save the file.

The image looks like the preview: frame offsets and colors are applied, including the curves that glide them between keyframes. Collision shapes, guides and the origin are not drawn.

## Several Animations

Selecting multiple animations stacks them in one image, with the animation selected last on top, the same as the preview.

The image is as long as the longest selected animation. Shorter animations that loop repeat until the image ends, and shorter animations that do not loop hold their last frame.

## GIF or WebP

| | GIF | WebP |
| --- | --- | --- |
| Fastest frame rate | 50 fps | 60 fps |
| Transparency | On or off for each pixel | Full alpha, with soft edges |
| Colors | 256 | Millions |
| Support | Everywhere | Current browsers and chat apps, including Discord |

Choose GIF when the file has to open anywhere. Choose WebP for smooth 60 fps motion, soft edges or transparent backgrounds with antialiasing.

## Options

### Frame Rate

GIF stores each frame's delay in whole hundredths of a second, and browsers play a frame with a delay of 10 milliseconds or less at 100 milliseconds. That makes 50 fps (20 milliseconds per frame) the fastest usable GIF rate, so the GIF dialog offers 50, 30, 25 and 10 fps. WebP stores delays in milliseconds and also offers 60 fps.

Frames that look the same as the one before them are merged into a single longer frame, so an animation without glide curves produces about one frame per animation frame regardless of the frame rate.

### Background

**Transparent** makes the background see-through. In a GIF, transparency is on or off for each pixel, so soft or antialiased edges show a jagged fringe. Pixel art without partial transparency is not affected. A WebP keeps soft edges.

**Solid color** fills the background with a color. The color starts as the editor's canvas background.

### Size

The size option scales the image up by a whole number, from 1x to 8x, keeping pixels sharp. Pixel art is often too small to read when shared at its original size.

### Quality (WebP)

**Lossless** keeps every pixel exact, which suits pixel art. Turn it off to use the quality slider for a smaller file.

## Limits

A GIF holds at most 256 colors. Pixel art that uses 256 or fewer colors across the whole animation is saved exactly, and richer art is reduced to a shared palette of 256 colors.

A WebP can be at most 16383 pixels wide or tall, so a large sprite at a high size setting can exceed it. The editor reports this instead of writing a broken file.
