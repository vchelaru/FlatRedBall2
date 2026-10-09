# Build from Source

## Introduction

Most people should use a [downloaded release](install.md). Building from source is useful if you want to try unreleased changes or contribute to the AnimationEditor. This page shows how to build and run the editor on Windows, macOS, and Linux.

## Prerequisites

Building requires the **.NET 10 SDK**. See [Installing .NET 10](../README.md#installing-net-10) for installation instructions on each platform. To verify the installation, run `dotnet --version`. The version should start with `10.`.

## Get the Source

Clone the repository, then change to the AnimationEditor project folder.

```bash
git clone https://github.com/vchelaru/FlatRedBall2.git
cd FlatRedBall2/tools/AnimationEditorAvalonia/src/AnimationEditor.App
```

## Build and Run on Windows and Linux

Run the project with `dotnet run`. This builds the editor if needed, then launches it.

```bash
dotnet run
```

## Build and Run on macOS

On macOS, `dotnet run` launches the bare executable. The Dock shows the assembly name, `AnimationEditor`, but no icon. To get the correct Dock name and icon, launch the `.app` bundle that the build produces. The `run-mac.sh` script builds the project and opens the bundle.

```bash
./run-mac.sh
```

The script runs a Debug build by default. Pass `Release` to build in Release instead.

```bash
./run-mac.sh Release
```

The terminal stays busy until you close the editor window, the same as `dotnet run`.

To launch the bundle manually after any `dotnet build`, use `open`.

```bash
open bin/Debug/net10.0/AnimationEditor.app
```

## Troubleshooting: Build Fails Because a File Is Locked

If the editor is still running when you build, MSBuild cannot replace the executable and the build fails with an error like the following.

```
error MSB3027: Could not copy "...apphost.exe" to "bin\Debug\net10.0\AnimationEditor.App.exe".
              The file is locked by: "AnimationEditor.App (XXXXX)"
```

The same problem can also appear as `MSB3021: Unable to copy file`. Close the editor window and build again. If the window is already closed but the error persists, a stray process is still holding the file. On Windows, end it in **Task Manager ▸ Details** by selecting `AnimationEditor.App.exe` and clicking **End Task**, or run the following in PowerShell.

```powershell
Get-Process -Name "AnimationEditor.App" -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.Id -Force }
```

Once the process is gone, the build succeeds.

## Where to Go Next

Once the editor is running, follow the [Quick Start](quick-start.md).
