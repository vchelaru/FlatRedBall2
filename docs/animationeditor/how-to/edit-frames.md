# Edit Frames

## Add Frames

To add a frame, mouse over an animation and press the **+** button to add a new frame.

<figure><img src="../../.gitbook/assets/edit-frames-add-frame-button.png" alt="Add Frame button shown when hovering over the Walk animation"><figcaption></figcaption></figure>

If your animation doesn't have any frames yet, you can drag an image from the **Images** tab onto the animation. This adds a new frame using the dragged image.

<figure><img src="../../.gitbook/assets/edit-frames-drop-image-on-animation.gif" alt="Dragging an image from the Images tab onto an empty animation"><figcaption></figcaption></figure>

If **Grid** is enabled, and you have at least one frame already set with a texture, additional frames can be added by holding down **Ctrl** and clicking cells in the grid.

<figure><img src="../../.gitbook/assets/edit-frames-ctrl-click-grid.gif" alt="Ctrl+clicking grid cells to add frames"><figcaption></figcaption></figure>

Existing frames can also be copied and pasted with **Ctrl+C** and **Ctrl+V** (**⌘C** and **⌘V** on macOS), or the right-click **Copy** and **Paste** menu items.

Frames can also be duplicated with **Ctrl+D** (**⌘D** on macOS), or the right-click **Duplicate** menu item.

## Delete Frames

To delete a frame, right-click the frame and select **Delete Frame**.

<figure><img src="../../.gitbook/assets/edit-frames-delete.png" alt="Frame right-click menu with Delete Frame highlighted"><figcaption></figcaption></figure>

Alternatively, press **Delete** (**Fn+Delete** on Mac laptops) to delete the selected frame.

## Change What Part of the Image a Frame Shows

To change the part of an image that is displayed, select a frame then drag its region around in the editor window. If **Grid** is enabled, movement snaps to the grid.

<figure><img src="../../.gitbook/assets/edit-frames-drag-region.gif" alt="Dragging a frame's region in the editor window"><figcaption></figcaption></figure>

If **Grid** is enabled, you can also double-click a cell to change coordinates. This works even when clicking inside the current bounds of a frame. This is especially useful when an entire image is selected.

<figure><img src="../../.gitbook/assets/edit-frames-double-click-cell.gif" alt="Double-clicking a grid cell to move the frame to it"><figcaption></figcaption></figure>

Alternatively you can edit the coordinate values in the **Inspector** tab.

<figure><img src="../../.gitbook/assets/edit-frames-coordinates.png" alt="Coordinates section of the Inspector tab"><figcaption></figcaption></figure>

## Reorder Frames

To reorder a frame, drag it to the desired location. Note that frame names are always in numerical order.

<figure><img src="../../.gitbook/assets/edit-frames-reorder.gif" alt="Dragging a frame to a new position"><figcaption></figcaption></figure>

Alternatively, you can hold **Alt** (**⌥ Option** on macOS) and press the **Up** or **Down** arrow key to reorder the selected frame.

## Edit Several Frames at Once

To edit multiple frames, select multiple frames by holding down **Shift** or **Ctrl** (**⌘** on macOS) and clicking the desired frames.

<figure><img src="../../.gitbook/assets/edit-frames-multi-select.gif" alt="Selecting several frames"><figcaption></figcaption></figure>

Once selected, frame values can be adjusted through the **Inspector** tab. If the values are not all equal, the text box shows **(mixed)**. Changing these values sets the same value for all frames.

<figure><img src="../../.gitbook/assets/edit-frames-multi-select-inspector.png" alt="Inspector showing mixed values for several selected frames"><figcaption></figcaption></figure>

Multiple selected frames can also be moved in the editor window.

<figure><img src="../../.gitbook/assets/edit-frames-multi-select-move.gif" alt="Moving several selected frames together in the editor window"><figcaption></figcaption></figure>

## Change a Frame's Image

An image can be changed by dragging a file from the **Images** tab onto the frame.

<figure><img src="../../.gitbook/assets/edit-frames-drop-image-on-frame.png" alt="Dragging an image from the Images tab onto a frame"><figcaption></figcaption></figure>

An image can also be dragged from the file system onto the frame. Note that if the dragged image is not relative to the current animation file, a popup asks whether to copy the file or reference it in its current location.

<figure><img src="../../.gitbook/assets/edit-frames-copy-or-keep-dialog.png" alt="Popup asking whether to copy an image from outside the project or keep it where it is"><figcaption></figcaption></figure>

Images can also be set by changing the **Texture** value in the **Inspector** tab, or by clicking its **…** button to browse for a file.

<figure><img src="../../.gitbook/assets/edit-frames-texture-field.png" alt="Texture field in the Inspector tab"><figcaption></figcaption></figure>

## Flip a Frame

To flip a frame, toggle the flip buttons in the **Inspector** tab. Frames can be flipped horizontally, vertically, or diagonally.

<figure><img src="../../.gitbook/assets/edit-frames-flip-buttons.png" alt="Flip Horizontal, Flip Vertical, and Flip Diagonal buttons in the Inspector tab"><figcaption></figcaption></figure>

## Change How Long a Frame Shows

A frame's length can be changed with the **Length** value in the **Inspector** tab. Length values are in seconds, with a default of 0.1 seconds.

<figure><img src="../../.gitbook/assets/edit-frames-length.png" alt="Length field in the Timing section of the Inspector tab"><figcaption></figcaption></figure>

{% hint style="info" %}
Changing a frame's length changes its width in the timeline.
{% endhint %}

<figure><img src="../../.gitbook/assets/edit-frames-length-timeline.png" alt="A longer frame taking more space in the timeline"><figcaption></figcaption></figure>

## Tint a Frame

Color values can be applied to a frame by changing the color **Mode** and values in the **Inspector** tab.

<figure><img src="../../.gitbook/assets/edit-frames-color-section.png" alt="Color section of the Inspector tab"><figcaption></figcaption></figure>

R, G, and B values require a **Mode** to be selected, or they have no effect on the color.

### Alpha

The A value represents `alpha`, or `opacity`. A value of 255 represents full opacity. A value of 0 is fully transparent.

Alpha can be modified in the **Inspector** tab.

<figure><img src="../../.gitbook/assets/edit-frames-alpha.png" alt="Frame drawn partly transparent with A set to 50"><figcaption></figcaption></figure>

### Multiply

Multiply, also sometimes called `modulate`, darkens the color of a frame. These values are *normalized* to 0-1 values, and then multiplied to the color of a frame. Values of 255 do not affect the color of the frame. Values of 0 completely remove the color from the frame.

For example, if Multiply with (R, G, B) values of (255, 0, 0) are set, then the red channel is still fully drawn, but the green and blue channels are completely removed from the frame.

Effective color values appear in the tree view, Animations project tab, and preview.

<figure><img src="../../.gitbook/assets/edit-frames-multiply.png" alt="Multiply with R, G, B of 255, 0, 0 shown in the tree, timeline, and preview"><figcaption></figcaption></figure>

### Add

Add can brighten or darken a frame, depending on whether positive or negative values are used. These values are directly added to a color value. A value of 255 sets that respective channel to its max value. A value of -255 completely removes the channel. Values in between can be used to fine-tune the appearance of a frame.

<figure><img src="../../.gitbook/assets/edit-frames-add.png" alt="Add with G of 255 brightening the frame"><figcaption></figcaption></figure>

### Value Cascading

{% hint style="info" %}
By default, frame values are unset, so their effective value is displayed in the R, G, B, and A text boxes.
{% endhint %}

<figure><img src="../../.gitbook/assets/edit-frames-color-unset.png" alt="Unset color values shown as grey placeholders"><figcaption></figcaption></figure>

Values cascade from one frame to the next. In other words, if a value is set on one frame, but subsequent frames do not set that value, then that value is inherited.

In the following image, Frame 1 sets the (R, G, B) values to (50, 100, 150). Frame 2 inherits these values, as shown in the **Inspector** tab and also the timeline and preview.

<figure><img src="../../.gitbook/assets/edit-frames-color-inherited.png" alt="Frame 2 inheriting color values from Frame 1"><figcaption></figcaption></figure>

## Offset a Frame

A frame's offset can be adjusted by changing its **Relative X** and **Relative Y** values in the **Inspector** tab. The preview window displays offsets as they are applied. Enabling the **Origin** option can help you align your sprites.

<figure><img src="../../.gitbook/assets/edit-frames-relative-x.png" alt="Relative X of 16 moving the sprite right of the origin"><figcaption></figcaption></figure>

{% hint style="info" %}
A value of 0,0 indicates a perfectly-centered sprite. Positive X values move the sprite to the right, and positive Y values move the sprite up.
{% endhint %}

{% hint style="info" %}
Future versions of the AnimationEditor will allow you to control whether positive Y is up or down to match your desired coordinate system.
{% endhint %}

Offsets can also be changed by dragging the frame in the preview. Every frame in an animation can be adjusted at once by selecting the whole animation, and several animations can be adjusted together by selecting all of them. To line animations up against each other, see [Align Animations](align-animations.md).

<figure><img src="../../.gitbook/assets/edit-frames-drag-offset-in-preview.gif" alt="Dragging a frame in the preview to change its offset"><figcaption></figcaption></figure>