# Issue #982 — Automatic updates: release package discovery and safe staged replacement

## Problem

The Animation Editor currently checks GitHub for a newer release, then sends the user to the
release page. It does not download or install the correct published package, report installation
state, or provide a reliable restart path.

The published release workflow already emits platform-specific portable archives:

- `AnimationEditor-win-x64.zip`
- `AnimationEditor-linux-x64.tar.gz`
- `AnimationEditor-osx-x64.zip`
- `AnimationEditor-osx-arm64.zip`

Replacing a running application cannot be done safely in-process on every supported operating
system. The new release must therefore be unpacked into a temporary staging location, then use its
own executable to replace the old installation after the old process exits.

## Proposed resolution

Add an update foundation shared by the desktop editor:

1. The release client exposes the published assets alongside the release date and page URL.
2. A platform resolver selects only the archive matching the current operating system and CPU
   architecture. Unsupported combinations remain safely on the existing release-page flow.
3. A downloader streams the selected asset to a unique staging directory, reporting progress and
   deleting partial files when download or validation fails.
4. A secure extractor rejects archive entries that escape the staging directory. It locates the
   staged editor executable or macOS app bundle without trusting archive paths blindly.
5. The staged executable supports an internal post-exit update mode. It waits for the old editor
   process, replaces the installed files, restarts the installed editor, and reports a recoverable
   failure without deleting the existing installation first.

The phase is intentionally UI-independent. A follow-up phase will connect the existing banner and
About dialog to progress, failure text, and the explicit restart action only after this mechanism
is covered by automated tests.

## Features / stories

- As an editor user, a newer release can be matched to my platform without downloading a package
  for another operating system or CPU.
- As an editor user, a failed or interrupted download leaves my current installation usable and
  does not retain a mistaken partial package.
- As an editor user, an update archive cannot write outside its designated staging directory.
- As an editor user, choosing restart after a staged update replaces the application only after the
  old process is no longer running and then launches the updated installation.

## Steps

- [x] Read issue #982 and its comments (none at the time of planning).
- [x] Inspect the existing update checker, update banner, release workflow, and editor test seams.
- [ ] Extend release metadata and add a tested platform-asset resolver.
- [ ] Add a progress-reporting staged downloader with a fakeable HTTP boundary and failure cleanup
  tests.
- [ ] Add secure ZIP and tar.gz extraction to a unique staging directory, including path-traversal
  rejection tests.
- [ ] Add the internal post-exit update application mode and cover replacement/relaunch planning
  without launching a real process in unit tests.
- [ ] Build and run the Core and App update-focused tests; record any platform-specific manual
  verification still needed.
- [ ] Update this phase document and the plan index when the foundation lands.

## Risks and decisions

- Archive names are a release contract. The resolver must use the names produced by
  `.github/workflows/animation-editor.yml`; any packaging rename must update both sides.
- The updater must preserve the old installation until the staged package is completely downloaded
  and extracted. A failed update must never make the current editor unlaunchable.
- Windows locks the running executable. The replacement must be performed by the staged executable
  after it observes the old process exit, never by the editor currently showing the UI.
- macOS packages an `.app` bundle; the staging and replacement paths must preserve that bundle as a
  unit. Linux and Windows packages use the published portable layout.
- Browser/WASM has no local installation to replace and is explicitly out of scope for this
  desktop-only phase.

Hand off to coder agent for implementation.
