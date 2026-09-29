# Quick Start

## Introduction

The AnimationEditor can work with single files, or it can help you manage a project which may use multiple animation files. This quick start shows how to work with projects since it simplifies finding and using existing PNG files.

This guide assumes that you have an existing folder with one or more images.

Before you open the project, the AnimationEditor shows an empty animation list.

<figure><img src="../.gitbook/assets/quick-start-empty-editor.png" alt="AnimationEditor with no folder open and an empty animation list"><figcaption></figcaption></figure>

## Open a Project Folder

To open a project folder, select **File ▸ Open Project Folder…**

<figure><img src="../.gitbook/assets/quick-start-open-project-folder.png" alt="File menu with Open Project Folder highlighted"><figcaption></figcaption></figure>

Once a project folder is opened, all images in the project appear in the **Images** tab. Any existing animations also appear in the **Animations** tab.

<figure><img src="../.gitbook/assets/quick-start-images-tab.png" alt="Images tab listing the PNG files in the opened project folder"><figcaption></figcaption></figure>

## Add an Animation

Click the **Add Animation** button to add a new animation.

<figure><img src="../.gitbook/assets/quick-start-add-animation-button.png" alt="Add Animation button highlighted below the animation list"><figcaption></figcaption></figure>

Give the animation a name. It now displays as an empty animation.

<figure><img src="../.gitbook/assets/quick-start-new-animation.png" alt="New empty animation named Coin in the animation list"><figcaption></figcaption></figure>

You can add frames by hovering over the animation and pressing the **+** button.

<figure><img src="../.gitbook/assets/quick-start-add-frame-button.png" alt="Add Frame button shown when hovering over the Coin animation"><figcaption></figcaption></figure>

Drag the desired image from the **Images** tab onto the new frame to use the image on the animation frame.

<figure><img src="../.gitbook/assets/quick-start-drop-image-on-frame.gif" alt="Dragging Items.png from the Images tab onto the new frame"><figcaption></figcaption></figure>

You can save at any time with **File ▸ Save**. Auto-save starts after the first save, so saving early protects your work.

## Edit Frames

To edit a frame, drag the handles or the body of the frame in the editor window. Clicking the **Grid** option makes it easier to work with sprite sheets which are grid-aligned.

<figure><img src="../.gitbook/assets/quick-start-edit-frame-grid.png" alt="Frame selected on the sprite sheet with Grid turned on"><figcaption></figcaption></figure>

## Add More Frames

To add more frames, either press the **+** button, or copy and paste an existing frame (**Ctrl+C**, **Ctrl+V** on Windows and Linux; **⌘C**, **⌘V** on macOS).

<figure><img src="../.gitbook/assets/quick-start-add-more-frames.gif" alt="Adding more frames to the Coin animation"><figcaption></figcaption></figure>

Frames can be moved by dragging the frame. The mouse wheel or touchpad can be used to zoom in closer to make it easier to work with frames.

<figure><img src="../.gitbook/assets/quick-start-move-frames.gif" alt="Dragging frames to new cells on the sprite sheet while zoomed in"><figcaption></figcaption></figure>

## Watch It Play

Click the animation's name to play it in the preview panel.

<figure><img src="../.gitbook/assets/quick-start-preview.gif" alt="Selecting the Coin animation to play it in the preview"><figcaption></figcaption></figure>

## Save

Files must be initially saved by clicking **File ▸ Save**. These files should be saved in your game's project, such as in the same folder as your `.png` image files.

Once a file is saved, the AnimationEditor automatically saves any changes.

<figure><img src="../.gitbook/assets/quick-start-auto-save.png" alt="Status bar showing Auto Save On after the file is saved"><figcaption></figcaption></figure>

## Next Steps

* [Edit Animations](how-to/edit-animations.md) covers renaming, duplicating, flipping, and changing an animation's speed.
* [Edit Frames](how-to/edit-frames.md) covers adding, copying, reordering, and timing frames, plus grid snapping and exact pixel coordinates.
* [Preview an Animation](how-to/preview-an-animation.md) covers playback speed and showing the previous frame (onion skin).
