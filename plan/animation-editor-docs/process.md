# AnimationEditor Docs: Collaboration Process (temporary)

Delete this file and outline.md before PR #1229 merges.

## Who does what
Victor writes each page. Claude adds images and links, then reviews.

## Markers
- `Image: <file>` — a screenshot in `C:\Users\Vic Personal\Documents\ShareX\Screenshots\2026-09`.
  A line directly after it is the caption.
- `Link` — link the text before it. A URL on the line means use that URL; no URL means link the
  best-matching docs page. If no page clearly fits, ask.

## Notes
A paragraph starting with "Note" becomes a GitBook hint block, with the "Note that" / "Note -" lead-in
dropped (the block already says it's a note):
`{% hint style="info" %}` … `{% endhint %}`. Same syntax as the Gum docs.

## Placing images
- Copy to `docs/.gitbook/assets/` as `<page>-<what>.png` (or `.gif`).
- Use GitBook figure markup: `<figure><img src="../.gitbook/assets/x.png" alt="…"><figcaption><p>caption</p></figcaption></figure>`.
  Caption given: it goes in both `alt` and `figcaption`. No caption: write `alt` from the image,
  leave `figcaption` empty.
- Path depth: `../` from `animationeditor/`, `../../` from its subfolders.

## Review after placing
Fix silently, without calling them out: typos, wrong UI labels (check against the code), heading
style (title case, imperative verbs), bold UI names, `File ▸ Item` menu paths, Mac equivalents for
every shortcut (Ctrl → ⌘, Alt → ⌥ Option, F-keys and Delete → Fn+ on Mac laptops), verifying
each shortcut against the hotkey table in `MainWindow.axaml.cs` (`Id = "…"` entries), and links to sections on other docs pages.

Suggest, don't apply, until Victor says which to fix: anything that changes what the page says or
how it's organized, screenshots that don't match the text, and missing coverage.

## Commits
Commit and push each page to `animation-editor-docs-structure` (PR #1229). Don't merge until every
page has content.
