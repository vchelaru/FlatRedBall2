# Decision: browser modal dialogs (#756)

Status: **Implemented**, pending live-browser visual verification.

## Decision

Browser dialogs use an in-page Avalonia overlay, not `Window.ShowDialog`.

The browser host runs under `ISingleViewApplicationLifetime`: its root is a `Control`, not a
`Window`, and Avalonia does not support opening a second `Window` on WebAssembly. Consequently
there is no valid owner for `ShowDialog`. The supported browser equivalent is a modal layer in
the single visual tree.

Dialog content and behavior are shared in `AnimationEditor.Views.Dialogs.EditorDialogs`.
Only presentation differs:

- Desktop uses `WindowEditorDialogHost`, which wraps the shared content in a modal `Window`.
- Browser uses `EditorDialogOverlay`, which adds a scrim and centered dialog card above the
  editor shell, blocks interaction with the editor, and completes the same asynchronous result.

Both hosts preserve Enter-to-confirm, Escape-to-cancel, initial focus, and close-as-cancel
semantics.

## Scope

The shared dialog layer now supplies:

- `IAppCommands.ConfirmAsync`
- `IAppCommands.PromptStringAsync`
- Adjust Frame Time
- Add Multiple Frames
- Adjust Offsets

The three chain commands are now present in the browser tree context menu. They invoke the same
`IAppCommands` operations as desktop, so mutation, undo, refresh, and save behavior remain in
Core rather than being duplicated by either host.

For Adjust Offsets, each host supplies a texture-height resolver. Browser resolves the frame's
bitmap through `ThumbnailService`; desktop preserves its wireframe bitmap behavior.

## Verification

- Headless tests cover overlay confirm/cancel behavior, the three browser menu entries, and a
  confirmed Add Multiple Frames mutation.
- The complete Animation Editor solution builds with zero warnings and errors, including the
  `net10.0-browser` WASM target.
- Live-browser verification should confirm card styling, keyboard focus, background input
  blocking, and all three chain-dialog workflows.
