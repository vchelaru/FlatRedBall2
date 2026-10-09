# Development Notes

## Building and Running Tests

```
cd tools/AnimationEditorAvalonia
dotnet build
dotnet test
```

---

## Dogfooding the editor without launching it

`tests/AnimationEditor.App.Tests/Dogfood/` drives the real `MainWindow` headlessly with simulated
clicks, drags, keys and scripted dialogs, so a change can be exercised end to end while the
machine is in use and without starting the app. Its README covers the harness, the scenario shape,
the find-pin-fix loop and the headless gotchas.

```
dotnet test tests/AnimationEditor.App.Tests --filter "FullyQualifiedName~Dogfood"
```

---

## Running the Editor and Build Troubleshooting

Running on macOS (`run-mac.sh`, for the Dock name and icon) and the "file is locked by another
process" build failure are covered in the user docs:
[Build from Source](../../../docs/animationeditor/build-from-source.md).

---

## Writing Tests: Cross-Platform Absolute Paths

**Never use hardcoded Windows paths** (`@"C:\..."`, `@"D:\..."`, etc.) in test files. Tests run on both Windows and Linux CI runners; Windows paths cause `FileNotFoundException` or path-comparison failures on Linux.

Use the `TestPaths` helper class instead:

```csharp
// Good — resolves to C:\TestRoot\... on Windows, /TestRoot/... on Linux
TestPaths.Abs("textures", "sprite.png")

// For paths that need a distinct drive/root from Abs()
TestPaths.AltAbs("Downloads", "capybara.png")

// For a directory path (appends trailing separator)
TestPaths.AbsDir("project", "textures")

// For paths that must not be writable (write-failure tests)
TestPaths.InvalidPath("recovery.achx")
```

`TestPaths` is defined in each test project's root (e.g., `AnimationEditor.Core.Tests/TestPaths.cs`). Add `AbsDir` / `InvalidPath` to `AnimationEditor.App.Tests/TestPaths.cs` if needed there too.

## Measuring memory (`--memory-probe`)

The app can drive its own open/close cycles and record memory at each phase, so a leak claim is
measured rather than eyeballed in Task Manager:

```
AnimationEditor.exe --memory-probe --probe-file <file.achx> [--probe-cycles N]
                    [--probe-file2 <other.achx>] [--probe-keep-open] [--probe-out <path.ndjson>]
```

It writes one NDJSON line per snapshot (private bytes, managed heap, Skia CPU/font caches, Skia
GPU resource cache) plus a final per-cycle growth summary, then exits without saving settings.
Defaults to `%TEMP%/ae-memory-probe.ndjson`. `--probe-keep-open` skips the close step;
`--probe-file2` alternates two files so the run exercises two textures instead of re-focusing one
already-open tab.

**Only `Settled` (post-close, post-GC) rows are comparable across cycles.** Comparing raw
readings without forcing a collection measures how lazy the GC is, not what the app retains — at
these sizes there is no memory pressure, so gen2 may not run for many cycles and private bytes
drift upward with no leak present.

**Measure the standalone exe, not a debugger-hosted run.** Under Visual Studio the same session
reports several times the private bytes, because the debugger and Diagnostic Tools commit their
own memory into the target process.
