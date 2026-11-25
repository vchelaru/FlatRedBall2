# Work with Project Folders

## Introduction

Project folders simplify the creation and management of animation files. This page shows how to work with project folders and outlines some of the benefits that project folders provide.

## Open and Close a Project Folder

To open a project folder, select **File ▸ Open Project Folder…** Once a project folder is opened, the AnimationEditor displays all animation and image files in their respective tabs. Note that the AnimationEditor lists `.achj`, `.achx`, and `.tsx` files.

The AnimationEditor remembers your previously-opened project folder and automatically selects it the next time it is launched.

## Find Files (Images and Animations Tabs)

The **Images** and **Animations** tabs both display the full folder structure of your project, including sub-folders. You can browse your project's files or search for specific files by clicking the **search** icon.

Both tabs display preview icons for each file. For `.png` files the entire image is displayed. For animations, the first frame of the first animation is displayed.

## Open, Switch, and Close Files

To open a file in the **Animations** tab, click the file. Its name is displayed in italics to indicate that it will be replaced on the next click.

<figure><img src="../../.gitbook/assets/project-folders-preview-tab.png" alt="DagonAnimations.achx open in an italic preview tab after a single click"><figcaption></figcaption></figure>

If you want it to stay open, double-click the item in the **Animations** tab, or make an edit to the file.

<figure><img src="../../.gitbook/assets/project-folders-pinned-tab.png" alt="DagonAnimations.achx tab no longer in italics after being kept open"><figcaption></figcaption></figure>

To view an image file, double-click the image file. A double click is necessary so that single clicks do not accidentally open image files when attempting to [drag and drop an image on a frame](../quick-start.md).

<figure><img src="../../.gitbook/assets/project-folders-image-view.png" alt="ChibiCthulhuTiles.png opened in its own tab with the Analyze Image Usage button"><figcaption></figcaption></figure>

## Add Animation Files to a Project

You can add new animation files by right-clicking on a project's folder and selecting **New Animation File**.

Any changes made on disk, such as copying an existing animation without using the AnimationEditor, are automatically reflected in the project tabs immediately.

## Convert .achx Files to .achj

To convert one file, right-click an `.achx` file in the **Animations** tab and select **Convert to .achj**. To convert every `.achx` file in a folder and its sub-folders, right-click the folder and select **Convert Animations to .achj**. Files under `bin` and `obj` folders are skipped.

Conversion changes only the file format. Before anything is written, a dialog shows what the conversion does:

* **Animations** lists the files that become `.achj`, and the files that are left alone with the reason. A file is left alone if an `.achj` with the same name exists, if it can't be read, if its tab has unsaved edits that can't be saved, or if the `.achj` would not hold exactly the same data.
* **Sequences** lists the `.seqj` files that play a converted file. The conversion points them at the `.achj`, so they keep playing. A `.seqj` that can't be updated, for example because it is read-only, is listed as one that stops playing.

Click a row with an arrow to see its details. Click **Convert** to run what the dialog shows, or **Cancel** to leave every file as it is. If the result differs from what the dialog shows, the AnimationEditor tells you what is different.

The AnimationEditor searches the project folder for sequences. Sequences in other folders, and game code that loads an `.achx` by name, are not changed.

### Go Back to .achx

The `.achx` files and the `.seqj` files that the conversion changes are moved to the Recycle Bin. To go back:

1. Restore the `.achx` files and the `.seqj` files from the Recycle Bin. Replace the `.seqj` files that are there now.
2. Delete the new `.achj` files.

Edits made to a `.seqj` after the conversion are not in the restored file.

## Find Which Animations Use an Image

If a project is open, you can track which portions of an image are used by animations in your project. To do this, open an image (double click it) to view it, then click **Analyze Image Usage**. Each animation is displayed in a different color.

<figure><img src="../../.gitbook/assets/project-folders-analyze-image-usage.png" alt="Analyze Image Usage outlining each animation's frames on the image in a different color"><figcaption></figcaption></figure>

Click on any frame to open the corresponding animation.

<figure><img src="../../.gitbook/assets/project-folders-open-from-usage.gif" alt="Clicking a highlighted frame to open its animation"><figcaption></figcaption></figure>

## Work Without a Project Folder

The AnimationEditor supports working directly with files without first selecting a project folder. You can create, save, and load files directly. The AnimationEditor remembers previously-opened files when it is closed so you can resume work.

Quick edits and one-off files can be worked with, but the recommended workflow is to load proejcts since it simplifies finding files, adding new files, and tracking which parts of a png is used by animations.