# Install

## Introduction

The AnimationEditor is distributed as a self-contained download, so you do not need to install .NET to use it. This page shows how to download and launch it on Windows, macOS, and Linux. To build the editor from source instead, see [Build from Source](build-from-source.md).

## Download

All downloads are on the [latest release](https://github.com/vchelaru/FlatRedBall2/releases/latest) page. Pick the file for your platform.

| Platform | File |
|---|---|
| Windows (installer) | `FlatRedBall2.AnimationEditor-win-Setup.exe` |
| Windows (portable) | `FlatRedBall2.AnimationEditor-win-Portable.zip` |
| macOS (Apple Silicon) | `AnimationEditor-osx-arm64.zip` |
| macOS (Intel) | `AnimationEditor-osx-x64.zip` |
| Linux (x64) | `AnimationEditor-linux-x64.tar.gz` |

## Windows

Run `FlatRedBall2.AnimationEditor-win-Setup.exe`. The installer registers the AnimationEditor for `.achj` and `.achx` files so double-clicking either type opens it. See [Opening Files from Windows](readme.md#opening-files-from-windows) for details.

Windows SmartScreen warns about the installer on first run because the AnimationEditor binaries are not signed. Select **More info ▸ Run anyway** to continue.

To skip the installer, extract `FlatRedBall2.AnimationEditor-win-Portable.zip` and run `AnimationEditor.exe`. The portable build does not register file associations.

## macOS

Download the zip that matches your Mac, either Apple Silicon (M1 and later) or Intel, and double-click it to extract `AnimationEditor.app`. Drag the app to your **Applications** folder.

The app is not notarized by Apple, so macOS blocks it the first time you open it. To allow it:

1. Double-click `AnimationEditor.app`. macOS shows a message that it cannot verify the app.
2. Open **System Settings ▸ Privacy & Security**.
3. Scroll to the **Security** section and click **Open Anyway** next to the AnimationEditor message.
4. Confirm by clicking **Open**.

macOS remembers your choice, so this is only needed once per download.

## Linux

Extract the archive and run the `AnimationEditor` executable inside it.

```bash
mkdir AnimationEditor
tar -xzf AnimationEditor-linux-x64.tar.gz -C AnimationEditor
./AnimationEditor/AnimationEditor
```

## Updating

The **About** dialog has a **Check for Updates** button. You can also download the latest release and replace the old files.

## Where to Go Next

Once the editor is running, follow the [Quick Start](quick-start.md).
