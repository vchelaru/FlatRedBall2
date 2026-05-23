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
