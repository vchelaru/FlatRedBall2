# Decision: automated screenshot capture harness (#636)

Status: **Decided and implemented** (see `tests/AnimationEditor.DocScreenshots/`).
Related: [#636](https://github.com/vchelaru/FlatRedBall2/issues/636).

## Problem

`WireframeControl`/`PreviewControl.RenderToBitmap` already give off-screen, deterministic pixel
capture — but only for those two controls, which draw directly onto an SkiaSharp canvas and bypass
Avalonia's compositor entirely. Most documentation task pages center on the tree view and
inspector panel, which have no equivalent capture path.

## Capture mechanism: `CaptureRenderedFrame`, not `RenderTargetBitmap.Render`

The obvious-looking approach — `new RenderTargetBitmap(size).Render(visual)`, Avalonia's normal
API for snapshotting any control — **silently writes an empty PNG under the headless platform**.
It never pumps the headless render-timer tick, so `Render` runs against a compositor that hasn't
produced a frame yet, and `Save` writes without error. `TopLevel.CaptureRenderedFrame()`
(`Avalonia.Headless`) is the platform's own supported capture path: it forces a render-timer tick
before capturing, so it actually contains pixels.

`ScreenshotCapture.Capture` (`tests/AnimationEditor.DocScreenshots/ScreenshotCapture.cs`) always
captures the owning `TopLevel`'s frame via `CaptureRenderedFrame`, then — for a sub-control — crops
to that control's bounds by drawing the frame into a fresh `RenderTargetBitmap` with
`DrawingContext.DrawImage(source, sourceRect, destRect)`. That draw is a plain image blit, not a
visual-tree render, so it isn't subject to the same "never pumped" problem.

## Why a separate test project, not a mode flag on `AnimationEditor.App.Tests`

This was the open question from the issue. `Avalonia.Headless.AvaloniaHeadlessPlatformOptions`
defaults `UseHeadlessDrawing` to `true` — a no-op drawing recorder, chosen for speed, under which
`CaptureRenderedFrame` always returns null. The ~90 existing files in `AnimationEditor.App.Tests`
never need real pixels (they assert on control-tree state directly, or go through
WireframeControl/PreviewControl's own SkiaSharp render path), so flipping that default assembly-wide
would slow down the whole suite for a capability only a handful of new tests need. `Avalonia.Headless`
wires its `AvaloniaTestApplicationAttribute` at the assembly level, so there's no way to opt one test
class out of the assembly's setting — a second assembly (`AnimationEditor.DocScreenshots`), with its
own `TestAppBuilder` setting `UseHeadlessDrawing = false` and `.UseSkia()`, was the only way to keep
both.

## Dialog spike: does headless capture work for modal dialogs?

Yes — see `DialogScreenshotSpikeTests.AboutDialog_CanBeCapturedHeadlessly`. `ShowDialog` returns a
`Task` that only completes when the dialog closes; a synchronous test calls it without awaiting,
flushes the dispatcher (`Dispatcher.UIThread.RunJobs()`, which runs layout for the now-open dialog),
captures the dialog window like any other `TopLevel`, then closes it to complete the pending `Task`.
No special-casing needed relative to a plain window.

## What this issue does not cover

Actual documentation-page screenshot content (Timing, Offsets, Collision, Texture Coordinates, Color
Operations, Layering Animations) is follow-on work — `DocScreenshotManifest`
(`tests/AnimationEditor.DocScreenshots/DocScreenshotGeneratorTests.cs`) has three representative
scenarios (tree view, inspector panel, full window chrome) proving the harness end-to-end; doc pages
add their own entries to that manifest.
