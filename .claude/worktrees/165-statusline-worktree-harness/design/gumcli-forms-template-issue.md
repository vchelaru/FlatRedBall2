# `gumcli new` (default `forms` template) crashes: missing `EventExport/gum_events.json`

## Summary

Running `gumcli new <path>` with the default `forms` template throws an unhandled `InvalidOperationException` because the embedded `manifest.txt` references `EventExport/gum_events.json`, but that file does not exist on disk in `Tools/Gum.ProjectServices/Templates/FormsTemplate/EventExport/` and is therefore not embedded into the assembly.

The `empty` template works.

## Version

`gumcli v2026.4.5.1+d6734463c57776303fe1e511c41f3c45567508ce`

## Repro

```bash
gumcli new Content/GumProject/GumProject.gumx
```

(equivalent to `gumcli new Content/GumProject/GumProject.gumx --template forms`)

## Actual

```
Unhandled exception: System.InvalidOperationException: Forms template resource not found:
'Gum.ProjectServices.Templates.FormsTemplate.EventExport.gum_events.json'.
The manifest may be out of sync with the embedded resources.
   at Gum.ProjectServices.FormsTemplateCreator.ExtractResource(Assembly assembly, ...)
   at Gum.ProjectServices.FormsTemplateCreator.Create(String filePath)
   at Gum.Cli.Commands.NewCommand.Execute(...)
```

Exit code: 1. No project files are created.

## Expected

A new Gum project is scaffolded under the supplied path, including all Forms controls, behaviors, and the `EventExport/gum_events.json` template file.

## Workaround

```bash
gumcli new Content/GumProject/GumProject.gumx --template empty
```

The `empty` template still copies all Forms components, behaviors, and standards, so it's effectively a complete project — the only thing it skips is the missing `EventExport/gum_events.json` step.

## Root cause

In the Gum repo:

- `Tools/Gum.ProjectServices/Templates/FormsTemplate/manifest.txt` lists `EventExport/gum_events.json`.
- `Tools/Gum.ProjectServices/Templates/FormsTemplate/EventExport/` is empty (only contains an empty subfolder, no `gum_events.json`).
- `Tools/Gum.ProjectServices/Gum.ProjectServices.csproj` embeds `Templates\FormsTemplate\**\*` — but with the file missing on disk, the resource isn't embedded.
- At runtime `FormsTemplateCreator` reads `manifest.txt`, iterates entries, and calls `ExtractResource(...)` for each. The first miss throws.

## Suggested fix

One of:

1. **Add the missing file** — commit an `EventExport/gum_events.json` (presumably an empty `[]` or a default-events stub) so the manifest matches what's embedded.
2. **Remove the entry from `manifest.txt`** — if `gum_events.json` is meant to be optional/generated later, drop it from the manifest list and have `FormsTemplateCreator` tolerate optional entries.
3. **Make `ExtractResource` skip missing-but-listed entries with a warning** instead of throwing — defends against this class of manifest drift in the future.

The cleanest of the three is probably (1) — keep the manifest authoritative and ensure all listed files exist in the source tree.
