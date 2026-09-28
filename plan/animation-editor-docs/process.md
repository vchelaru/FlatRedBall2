# AnimationEditor Docs: Collaboration Process (temporary)

Delete this file and outline.md before PR #1229 merges.

## Who does what
Victor writes each page. Claude adds images and links, then reviews.

## Markers
- `Image: <file>` — a screenshot in `C:\Users\Vic Personal\Documents\ShareX\Screenshots\2026-09`.
  A line directly after it is the caption.
- `Link` — link the text before it. A URL on the line means use that URL; no URL means link the
  best-matching docs page. If no page clearly fits, ask.

## Placing images
- Copy to `docs/.gitbook/assets/` as `<page>-<what>.png` (or `.gif`).
- Use GitBook figure markup: `<figure><img src="../.gitbook/assets/x.png" alt="…"><figcaption><p>caption</p></figcaption></figure>`.
  Caption given: it goes in both `alt` and `figcaption`. No caption: write `alt` from the image,
  leave `figcaption` empty.
- Path depth: `../` from `animationeditor/`, `../../` from its subfolders.

## Review after placing
Suggest, don't apply, until Victor says which to fix:
- Typos, heading style (title case, imperative verbs), bold UI names, `File ▸ Item` menu paths.
- Shortcuts for both Ctrl (Windows/Linux) and ⌘ (macOS).
- Screenshots that don't match the text, especially after sections move.
- Missing links to other docs pages.

## Commits
Commit and push each page to `animation-editor-docs-structure` (PR #1229). Don't merge until every
page has content.
