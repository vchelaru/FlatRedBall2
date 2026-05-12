# Feedback for `Gum/docs/cli/pack.md`

Notes from a first real consumer (FlatRedBall2 / Solitaire sample) wiring `gumcli pack` into a production pipeline. The doc is solid on the *what*; this is feedback on the parts that bit us during integration.

Two pieces of feedback — one for the `pack.md` doc, one for the `GumBundleLoader.cs` source comments. The matching code-side fix has already landed in `GumCommon/Bundle/GumBundleLoader.cs` (the loader now reads the `.gumpkg` through `FileManager.GetStreamForFile` so a host-installed `CustomGetStreamFromFile` hook is honored on TitleContainer-backed platforms).

---

## 1. `pack.md` — "Loading a `.gumpkg` at runtime" needs a TitleContainer note

The current section says the runtime loader transparently switches to bundle mode when the loose `.gumx` is absent. That's true on desktop but not on platforms without a real filesystem (Blazor WebAssembly being the headline case; Android and iOS via TitleContainer in principle).

What's missing: the `.gumpkg` itself has to be readable by the loader. On TitleContainer-backed platforms there is no `File.OpenRead` — content lives behind `TitleContainer.OpenStream`. The loader reads through `FileManager.GetStreamForFile`, which routes:

- Android / iOS: hard-coded to `TitleContainer.OpenStream`
- Everywhere else: through `FileManager.CustomGetStreamFromFile` if the host installed one, otherwise straight `File.OpenRead`

So on Blazor WASM, **the host must install** `FileManager.CustomGetStreamFromFile = TitleContainer.OpenStream` (or equivalent) before calling `GumService.Initialize`, or the bundle file can't be read. This isn't currently called out in the docs and the failure mode is a `FileNotFoundException` deep in the loader after a successful build — easy to mistake for a content-deployment bug.

### Suggested addition

Add a subsection under "Loading a `.gumpkg` at runtime":

> #### TitleContainer-backed platforms (Blazor WASM, Android, iOS)
>
> Platforms without a writable filesystem load content through `TitleContainer`. Gum's loader reads the `.gumpkg` (and all loose-mode files) through `FileManager.GetStreamForFile`, which routes Android and iOS to `TitleContainer` automatically. **On every other TitleContainer-backed platform — Blazor WebAssembly being the common case — the host must install the hook explicitly:**
>
> ```csharp
> using ToolsUtilities;
> using Microsoft.Xna.Framework;
>
> // Install once before GumService.Initialize. Compose with any hook the host
> // (or another library) has already set so you don't clobber custom asset
> // bundling — the bundle loader itself uses the same compose-with-fallback
> // pattern when it installs its bundle hook on top of yours.
> if (FileManager.CustomGetStreamFromFile == null)
> {
>     FileManager.CustomGetStreamFromFile = TitleContainer.OpenStream;
> }
> ```
>
> Without this hook on Blazor WASM, both loose-mode `.gumx` loads and `.gumpkg` reads fail with `FileNotFoundException` at runtime even though the files were correctly published — the loader simply doesn't know how to reach them.

### Optional second note

Same section already has the .NET 7+ warning. Worth adding a sentence on the failure mode when both conditions go sideways:

> If your project targets `net6.0` or earlier **and** you publish only the `.gumpkg` (loose files excluded from output), the runtime falls back to loose-file resolution, finds nothing, and `GumService.Initialize` throws on a missing `.gumx`. The fix is to either bump to `net7.0`+ (recommended) or include the loose files in the published output.

---

## 2. `GumBundleLoader.cs` XML doc — clarify the seam

The class summary on `GumBundleLoader` mentions installing the `CustomGetStreamFromFile` hook to serve bundle entries, but does not say that **reading the `.gumpkg` itself** also flows through `FileManager.GetStreamForFile`. That distinction matters for hosts deciding when to install their own hook.

Suggested rewording (current text in italic; proposed underlined inline):

> Resolves whether a Gum project should load from loose `.gumx` + sibling files on disk or from a sibling `.gumpkg` bundle, and installs the `FileManager.CustomGetStreamFromFile` hook to serve bundle entries when needed.
>
> **Add:** _The bundle file itself is opened through the same `FileManager` seam, so on TitleContainer-backed platforms (Blazor WASM, Android, iOS) the host must have installed `CustomGetStreamFromFile` before calling Resolve, or the bundle read will fall through to the desktop `File.OpenRead` path and fail._
>
> Per the bundle plan §4: loose wins when both exist (dev convenience / hot reload). Production publishes only the bundle.

---

## 3. Smaller things noticed in passing

- **`pack.md` example output line widths**: the "Packed N files into ..." block has aligned spacing only at one decimal place of percent (`27.0%`); fine, just noting that the BlazorGL Solitaire sample compresses to **9.4%** which is satisfyingly extreme and could be a more compelling example than 27%.
- **The "Loose wins when both exist" callout** is great but appears only at the bottom of the runtime-loading section. Worth a one-liner near the top of the doc too — at least one of us tried `gumcli pack` followed by "is it actually using the bundle?" and had to dig into the loader source to confirm. A note in `pack.md`: *"To verify bundle mode is active, exclude the loose files from your build output. With both deployed, the loader silently uses loose."*
- **Exit code 1 ("dependency files missing")** — would be nice if `pack` printed *which* files were missing, the way `check` does. Currently the only signal is a non-zero exit code; you have to re-run with `gumcli check` to see the list. (Source-side feedback, not a doc thing — but maybe the doc could mention "if pack exits 1, run `gumcli check` to see which files are missing.")

---

## 4. Bug found during integration: relative-path resolution in `GumBundleLoader.Resolve`

Worth flagging because it'll bite anyone who follows the docs literally. `pack.md` says:

> ```csharp
> GumService.Default.Initialize(graphics, gumProjectFile: "MyProject/MyProject.gumx");
> ```

That's exactly how FRB2's Solitaire sample passed the path — relative, no `Content/` prefix, matching how `GumProjectSave.Load` treats it (it normalizes via `FileManager.MakeAbsolute` against `FileManager.RelativeDirectory`, defaulted to `"Content/"`).

`GumBundleLoader.Resolve` did **not** apply the same normalization — it ran `File.Exists(gumxPath)` and `File.Exists(bundlePath)` against the raw relative input, against process CWD. So with the .gumx absent and the .gumpkg deployed at `bin\.../Content/GumProject/GumProject.gumpkg`, the loader probed `bin\.../GumProject/GumProject.gumpkg`, missed, fell through to loose mode, and the downstream load surfaced "Could not find main project file" — a confusing error for a build that did pack and deploy correctly.

The fix landed in `GumBundleLoader.cs` (FRB2's linked Gum source): normalize via `FileManager.IsRelative` + `FileManager.MakeAbsolute` at the top of `Resolve`, before any `File.Exists` probe. Keeping for upstream merge into Gum.

Suggested test to add to `GumBundleLoaderTests`:

```csharp
[Fact]
public void Resolve_uses_FileManager_RelativeDirectory_when_input_is_relative()
{
    // Mirror the production path: GumProjectSave.Load normalizes through FileManager.RelativeDirectory.
    // Resolve() must do the same so the loose-vs-bundle probe runs against the right directory.
    string projectDir = Path.Combine(_tempDir, "Content", "GumProject");
    Directory.CreateDirectory(projectDir);
    WriteBundle(Path.Combine(projectDir, "GumProject.gumpkg"), new (string, byte[])[]
    {
        ("GumProject.gumx", Encoding.UTF8.GetBytes("<GumProjectSave />")),
    });

    string previousRelative = FileManager.RelativeDirectory;
    try
    {
        FileManager.RelativeDirectory = _tempDir;
        BundleResolution resolution = GumBundleLoader.Resolve("Content/GumProject/GumProject.gumx");
        resolution.UsedBundle.ShouldBeTrue();
    }
    finally
    {
        FileManager.RelativeDirectory = previousRelative;
    }
}
```

---

## 5. `FileManager.FileExists` does not consult `CustomGetStreamFromFile` on non-mobile

This was a separate Blazor / bundled-content failure mode that turned the bundle into "file is there but everything renders with the default font."

The deferred-font loader in `Gum/Wireframe/CustomSetPropertyOnRenderable.cs:1200` does this:

```csharp
if (ToolsUtilities.FileManager.FileExists(fileName))
{
    font = new BitmapFont(fileName);
}
else
{
    font = Text.DefaultBitmapFont;   // silent fallback
}
```

`FileManager.FileExists` (`ToolsUtilities/FileManager.cs:142`) on non-mobile platforms does a raw `File.Exists` and never probes through `CustomGetStreamFromFile`:

```csharp
if (IsMobile)
{
    // probes through CustomGetStreamFromFile
}
else
{
    return File.Exists(fileName);   // ← bundled fonts say "no"
}
```

Result: every bundled `.fnt` looks "missing" to the loader, the fallback to `Text.DefaultBitmapFont` is silent, and the user sees Solitaire with the wrong font everywhere — no exception, no warning. The exact same shape applies to any other consumer that uses `FileExists` as a guard before `GetStreamForFile`.

The fix landed in `ToolsUtilities/FileManager.cs`: the non-mobile branch now also probes through `CustomGetStreamFromFile` after a raw-`File.Exists` miss. One change at the right layer — every bundle-aware existence check benefits, not just the font loader.

This is worth a regression test that mirrors the production pattern: install a hook that resolves *only* one path, then assert `FileExists` on that path returns true.

---

## 6. Blazor WebAssembly cannot use the BCL `BrotliStream` or `System.Formats.Tar`

This was the most disruptive surprise. `.gumpkg` was meant to optimize Blazor first, but the format as shipped relies on two BCL packages that both throw `PlatformNotSupportedException` on browser-WASM:

- `System.IO.Compression.BrotliStream` — Mono does not link the native brotli library on WASM.
- `System.Formats.Tar` — depends on POSIX file metadata APIs that aren't surfaced on WASM.

A consumer publishing a `.gumpkg` to Blazor today will hit one of these in `GumBundleReader.Read` and the page won't load. The doc never warned us; we found this empirically.

We swapped both on the read side to pure-managed implementations:

- **`BrotliSharpLib`** — drop-in `BrotliStream` API, same on-disk format, ~2× slower decompress (one-time cost at game startup, negligible).
- **`SharpCompress`** — pure-managed tar reader. Same wire format. Note its `TarReader.OpenReader` does a format-sniff rewind that requires a seekable input, so the decompressed brotli stream has to be materialized into a `MemoryStream` first. (Bundles are small; this is fine.)

Both packages target `netstandard2.0`, so they work on every TFM Gum supports. The writer kept `System.IO.Compression.BrotliStream` and `System.Formats.Tar` since pack runs on desktop where native works and is faster.

`GumCommon.csproj` now carries:

```xml
<PackageReference Include="BrotliSharpLib" Version="0.3.3" />
<PackageReference Include="SharpCompress" Version="0.47.4" />
```

The XML doc on `GumBundleReader` was rewritten to call out the rationale.

`pack.md` should call out this constraint explicitly: **"On platforms that don't support native brotli or POSIX tar (notably Blazor WebAssembly), consumers must read the bundle through pure-managed equivalents."** Or, better, the upstream Gum decision should be: *the BCL packages stay only on the writer; the reader is permanently pure-managed.* Otherwise every consumer gets to discover this the hard way.

A test that would have caught this: spin up the reader on a `Browser`-flagged target, or simply assert it works against a `MemoryStream` without depending on framework brotli.

---

## 7. Rooted paths are produced by `MakeAbsolute` on WASM but rejected by `TitleContainer`

After fixing items 4-6, Blazor still threw on the `.gumpkg` load:

> Invalid filename. TitleContainer.OpenStream requires a relative path. (Parameter '/Content/GumProject/GumProject.gumpkg')

`FileManager.MakeAbsolute("GumProject/GumProject.gumx")` with `RelativeDirectory = "Content/"` produces `/Content/GumProject/...` on WASM (different `Path.Combine` semantics from Windows — leading slash gets prepended somewhere in the standardize-after-combine flow). Subsequent calls into `FromFileText` can produce a doubled prefix `//Content/...` if the path is then concatenated against another rooted `RelativeDirectory`.

TitleContainer rejects any path that `Path.IsPathRooted` reports as rooted, so both the single-slash and double-slash variants fail.

Two fixes layered on top of each other in our consumer:

1. **In FRB2's `CustomGetStreamFromFile` wrapper**: strip *all* leading separators (not just one) before delegating to `TitleContainer.OpenStream`. Any number of leading `/` or `\` are dropped, then the remaining relative path goes through.

2. **In Gum's `GumBundleLoader.TryMakeRelative`**: collapse runs of `/` in both `incomingPath` and `projectRoot` before doing the prefix-match. Without this, an incoming `//Content/GumProject/foo.fnt` won't match a `projectRoot` of `/Content/GumProject` and the bundled asset silently falls through to the host hook (which then can't find it loose either, because we shipped the bundle).

Both fixes landed locally. Item 2 is a Gum bug — the bundle hook should be robust to whatever `MakeAbsolute` produces on the platform. Item 1 is a host concern but worth documenting as a "what hosts must do for TitleContainer-backed platforms."

Suggested upstream test in `GumBundleLoaderTests`:

```csharp
[Fact]
public void TryMakeRelative_collapses_doubled_separators_in_incoming_path()
{
    // Reproduces the WASM path-shape: MakeAbsolute can produce a path with doubled
    // slashes after a Path.Combine against a RelativeDirectory that itself had a
    // leading separator. The bundle hook must still match this against projectRoot
    // or the bundled asset silently misses.
    // ... assert TryMakeRelative("//Content/GumProject/foo.fnt", "/Content/GumProject")
    //     returns "foo.fnt"
}
```

---

## Summary for Gum

The bundle format was solid; the integration story for non-desktop consumers had four cliff-edges (items 4-7) that each took a separate trip through the debugger to find. They share a theme: **the bundle reader assumes a desktop-shaped runtime**. Aligning the reader with what `GumProjectSave.Load` already tolerates (TitleContainer fallback, hook-aware existence checks, MakeAbsolute normalization, slash-collapsing in path matching) would close the gap. Tests against an explicit `Browser`-shaped harness — or even just a `RelativeDirectory != ""` harness — would have caught most of these.
