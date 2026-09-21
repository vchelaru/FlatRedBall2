---
name: animation-editor-rendering
description: SkiaSharp rendering & performance in the Animation Editor — GPU vs CPU draw paths, SKImage/SKBitmap gotchas, diagnosing slow frames. Triggers: ICustomDrawOperation, DrawFrameCore, WireframeControl.DrawOp, GrContext, SKImage, F3 diagnostics overlay, ThumbnailService.
---

# Animation Editor — Rendering & Performance

Where and how the Animation Editor draws pixels, and the SkiaSharp gotchas that come with it. Editor location/layout lives in the **`animation-editor`** skill; headless test discipline in **`animation-editor-testing`**.

Both panels render through a SkiaSharp `ICustomDrawOperation` on Avalonia's render thread (top: `WireframeControl.DrawOp.Render`; bottom: `PreviewControl.DrawFrameCore`). `lease.GrContext != null` means the GPU (ANGLE) path; null means CPU (software) — the two behave differently, so always know which you're on before reasoning about cost.

**A "used to be smooth, now it's slow" report is a git signal, not an architecture signal.** Before theorizing about the pipeline, `git log` the render files — a recent commit that changed *how an image is drawn* is far more often the cause than a long-standing pattern suddenly biting. Chasing the architecture first wastes rounds.

**Measure before guessing.** An on-canvas draw-time overlay (rolling ms/frame + a GPU/CPU tag) toggles with **F3** (`DiagnosticsEnabled` on each control, rendered by `DrawTimeOverlay`). Turn it on first: the ms reading plus the GPU/CPU tag localize the cost and rule out whole categories of hypothesis immediately.

**Landmine — a raster `SKImage` re-uploads to the GPU every frame.** An `SKImage` from `SKImage.FromBitmap` is CPU-resident; on the GPU path Skia re-uploads the *visible source region* on each draw, so cost scales inversely with zoom — **zoomed out is slower**, which misdirects toward mipmaps/filtering. Fix: let Skia keep the texture cached by raising the GPU resource-cache budget once per lease (`GRContext.SetResourceCacheLimit`), sized to hold the image. Do **not** hand-manage a GPU copy via `SKImage.ToTextureImage` held across frames — opening a menu/popup purges the `GRContext`, leaving that cached texture dangling so it draws nothing (blank/flicker of *only* the image, while vector draws in the same pass survive). Skia's own cache re-uploads correctly after a purge; a hand-held texture does not.

**Landmine — don't call `SKImage.Subset()` more than once against the same `SKImage` instance.** Caching one `SKImage.FromBitmap(sheet)` and repeatedly `.Subset()`-ing it (e.g. once per frame when cropping many frames off one sheet) crashes the process with an access violation — happened in `ThumbnailService.GetFrameThumbnail`. Use `SKBitmap.ExtractSubset(dst, rect)` instead: a pixel-buffer view (not a copy) on a fresh local bitmap each call, so nothing is shared to corrupt, at the same low cost.
