# Development Notes

## Building and Running Tests

```
cd tools/AnimationEditor/AnimationEditorAvalonia
dotnet build
dotnet test
```

---

## Running on macOS — Dock name and icon

On macOS, `dotnet run` launches the bare executable and the Dock shows
`AnimationEditor` (the assembly name). To get the full `Animation Editor`
label with the correct icon, launch via the `.app` bundle that the build
produces automatically:

```bash
# From the AnimationEditor.App project directory:
./run-mac.sh

# Or manually after any dotnet build:
open bin/Debug/net10.0/AnimationEditor.app
```

`run-mac.sh` builds the project and calls `open -W` (waits for the window to
close), giving the same terminal experience as `dotnet run`.

---

## Troubleshooting: Build Fails with "file is locked by another process"

**Symptom**

```
error MSB3027: Could not copy "...apphost.exe" to "bin\Debug\net8.0\AnimationEditor.App.exe".
              The file is locked by: "AnimationEditor.App (XXXXX)"
```

or

```
error MSB3021: Unable to copy file "...". The process cannot access the file
              '...AnimationEditor.App.exe' because it is being used by another process.
```

**Root cause**

The AnimationEditor app is still running. MSBuild cannot replace the executable while it is open.

**Fix — try these in order:**

1. **Close the AnimationEditor window** — the simplest fix; just close the UI.

2. **Kill via Task Manager** — open Task Manager → Details tab → find `AnimationEditor.App.exe` → End Task.

3. **Kill via PowerShell:**
   ```powershell
   Get-Process -Name "AnimationEditor.App" -ErrorAction SilentlyContinue |
       ForEach-Object { Stop-Process -Id $_.Id -Force }
   ```

4. **Kill by the PID shown in the error message** (e.g. PID 63072 above):
   ```powershell
   Stop-Process -Id 63072 -Force
   ```

Once the process is gone, re-run `dotnet build` / `dotnet test` and it will succeed immediately.

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
