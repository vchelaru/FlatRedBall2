# Development Notes

## Building and Running Tests

```
cd tools/AnimationEditor/AnimationEditorAvalonia
dotnet build
dotnet test
```

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
