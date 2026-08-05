# Duplication Audit — Avalonia Animation Editor (issue #695)

High-level inventory of duplicated logic and refactor wins in `tools/AnimationEditorAvalonia/`.
Line numbers are from the state of the tree at the time of the audit and are meant as landmarks,
not exact anchors — the clusters listed as landed below have since moved.

## Implementation status

All six clusters in section A have been extracted on this branch. What each one shipped:

| Cluster | Extracted to | Notes |
| --- | --- | --- |
| A1 | `Views/Controls/ZoomAnimator.cs` | Ported `PreviewControl`'s float-drift fix to `TextureViewport` (test-first) — the divergence this cluster flagged. |
| A2 | `Views/Controls/RevealHost.cs` | `PngPreviewControl` gained the `Step`/`Settle` test seams `WireframeControl` already had. |
| A3 | `Views/Controls/DiagnosticsOverlayHost.cs`, `DrawTimeOverlay.TimeAndDraw` | Settled the GPU/CPU-label inconsistency between the two panels. |
| A4 | `Views/Controls/IPanScrollTarget.cs`, `PanScrollBinder.cs` | Collapsed three `MainWindow` copies and all three suppression flags. `PreviewControl.SetPanX/Y` now take a raw scrollbar value like `TextureViewport`'s, so the two conventions became one. |
| A5 | `Views/Controls/EditorNotificationOverlay` (already existed) | `MainWindow` hosts it as `Notifications`; the desktop toast/banner code-behind and its XAML are gone. |
| A6 | `Views/Services/RevealInExplorerMenu.cs` | |

Section C (non-wins) was left alone as recommended.

## Motivating precedent

`49e159f Reuse RevealAnimation for wireframe selection bump instead of a parallel SelectionPop.`
— #542 first shipped a parallel `SelectionPop` helper even though #606's `RevealAnimation`
(`PngPreviewControl`) already encoded the same shrink-to-rest idea. The *math* was unified. The
*host* around the math (progress field, `DispatcherTimer`, `StepX`/`SettleX`, stop-on-settle,
`InvalidateVisual`) was not, and it is still duplicated. Every cluster below is a variation on
that same failure mode: a shared pure core exists in `AnimationEditor.Core`, but the Avalonia-side
plumbing that drives it was retyped.

**Structural constraint worth stating up front:** `AnimationEditor.Core` references neither
Avalonia nor SkiaSharp (see `AnimationEditor.Core.csproj`). Any shared *timer host* or *SKRect
helper* must live in `AnimationEditor.Views`, not Core. That is why these clusters were never
extracted into Core alongside `ZoomChase` / `RevealAnimation` / `CanvasTransform`.

---

## A. Highest-value duplication clusters

### A1. Smooth wheel-zoom host — duplicated and already diverged

**Where**

| Concern | `AnimationEditor.Views/Controls/TextureViewport.cs` | `AnimationEditor.Views/Controls/PreviewControl.cs` |
| --- | --- | --- |
| State fields (`_zoomTimer`, `_zoomAnimating`, `_zoomTarget`, `_zoomPivotVpX/Y`, `ZoomAnimIntervalSeconds`) | 260–265 | 64–69 |
| `StepZoomAnimation(float)` | 863–878 | 934–954 |
| `SettleZoomAnimation()` | 882–887 | 958–963 |
| `BeginAnimatedZoom(...)` | 1000–1007 | 972–979 |
| `CancelZoomAnimation()` | 1012–1017 | 984–989 |
| `ComputeTargetZoom(...)` | 1022–1028 | 994–1000 |
| `StartZoomTimer` / `StopZoomTimer` / `CreateZoomTimer` | 1030–1043 | 1028–1041 |
| `IsZoomAnimating` / `TargetZoom` / `SimulateWheelZoomBegin` | 845–854 | 917–925 |

`PreviewControl.cs:59` says it outright: *"Mirrors WireframeControl's #425 smooth zoom"*.
History confirms the copy: `5b60f23` (#425, wireframe) then `3c71c42` (#451, preview).

**Why it matters — this one has already bitten**

1. `PreviewControl.StepZoomAnimation` ends with a float-drift correction the other copy never got:

   ```
   // On the settling tick, snap the zoom scalar exactly onto the target. The per-tick factor
   // multiplications accumulate float drift (e.g. 1.5000001 instead of 1.5), which would make
   // a preset-stepping zoom button mis-read the current preset and fail to step (#451).
   if (settling) _zoom = _zoomTarget;
   ```

   `git log -S "_zoom = _zoomTarget"` returns exactly one commit — `3c71c42` (#451). It was never
   back-ported. `TextureViewport` accumulates the same per-tick factor products
   (`ZoomToward(pivot, next / _zoom)`) and `SetZoomPercent` (754–759) also goes through a
   *relative* `ZoomToward(newZoom / _zoom)`, so the wireframe/PNG panes carry the same latent
   preset-misread bug #451 fixed for the preview pane. The existing wireframe test only asserts to
   `precision: 5`, which is loose enough to hide it.
2. Zoom-out fallback multiplier is written two different ways: `1f / 1.25f`
   (`TextureViewport.cs:1026`) vs `0.8f` (`PreviewControl.cs:998`). Same value today; two things
   to remember to change tomorrow.

**Proposed extraction** — `AnimationEditor.Views/Controls/ZoomAnimator.cs`, a small
non-static class owning the five fields and the timer:

```csharp
sealed class ZoomAnimator(Func<float> getZoom, Action<float, float, float> applyFactor,
                          Action<float> snapZoom, int[]? presets)   // presets via a getter
{
    public bool IsAnimating { get; }
    public float Target { get; }
    public void Begin(float pivotVpX, float pivotVpY, bool zoomIn);
    public bool Step(float dtSeconds);
    public void Settle();
    public void Cancel();
    public void StartTimer();
}
```

Each control keeps its existing public `StepZoomAnimation` / `SettleZoomAnimation` /
`IsZoomAnimating` / `TargetZoom` as one-line delegations, so **no test changes are required** and
the deterministic test seam the issue wants to keep stays exactly as-is.

**Effort** M — ~90 duplicated lines per control, mechanical, fully covered by two existing test
files. **Recommendation: follow-up issue (draft B1 below).** Land the `_zoom = _zoomTarget` snap
for `TextureViewport` as a separate, test-first commit *before* the extraction, so the behavior
fix is reviewable on its own.

---

### A2. One-shot reveal timer host — the #542/#606 near-miss, round two

**Where**

| Concern | `PngPreviewControl.cs` (#606) | `WireframeControl.cs` (#542) |
| --- | --- | --- |
| Progress field + nullable timer | 37–38 | 334–335 |
| `Start…` (reset to 0, `??= Create…Timer()`, `Start()`) | 103–108 (`StartReveal`) | 513–519 (`BeginSelectionReveal`) |
| `Create…Timer()` at `RevealAnimation.DefaultIntervalSeconds` | 110–115 | 569–577 |
| `Step…` (`StepProgress`, stop at ≥ 1, `InvalidateVisual`) | 117–124 | 550–560 |
| `Settle…` (bounded loop to completion) | *absent* | 563–567 |
| Teardown on detach | 42 (ctor) | base-class teardown |

`WireframeControl.cs:512` documents the copy in its own summary: *"Resets reveal progress to 0 and
starts the timer (mirrors PngPreviewControl)."*

**Why it matters**

- This is the exact duplication #695 cites, one layer up. `RevealAnimation` unified the curve;
  the *driver* is still two hand-rolled copies plus a third timer host of the same shape
  (`WireframeControl` auto-pan, 638–651).
- The two copies are **asymmetrically tested**. `WireframeControl` exposes
  `StepSelectionReveal` / `SettleSelectionReveal` / `IsSelectionRevealAnimating` /
  `SelectionRevealProgress` and has a dedicated `WireframeSelectionRevealTests.cs` plus reveal
  assertions in `ChainSingleClickRevealTests` and `MultiChainSelectRevealTests`.
  `PngPreviewControl`'s reveal is entirely private: grepping the whole test tree for
  `SetDiffRegions` or `RevealProgress` on the PNG side returns **nothing**. The PNG bounce has no
  test at all. A shared host would hand it the same seam for free.
- A fourth "draw the eye to a box" surface is the obvious next request, and today there is nothing
  for it to reuse except by reading two sibling controls.

**Proposed extraction** — `AnimationEditor.Views/Controls/RevealHost.cs`:

```csharp
sealed class RevealHost(Action onTick)      // onTick == InvalidateVisual
{
    public float Progress { get; }          // 1 == settled
    public bool IsAnimating => Progress < 1f;
    public void Restart();                  // progress = 0, start timer
    public bool Step(float dtSeconds);      // StepProgress, auto-stop at 1
    public void Settle();                   // bounded loop
    public void Stop();
}
```

Keep every existing public `StepSelectionReveal` / `SettleSelectionReveal` /
`IsSelectionRevealAnimating` name as a delegation. Add the matching trio to `PngPreviewControl`
and cover the PNG bounce.

Fold in the small screen-space helpers while touching these files: `WireframeControl.SnapToScreen`
(253–258), `PngPreviewControl`'s inlined equivalent (161–163), and `TextureViewport.ToScreen`
(1186–1190) are three wrappers over `CanvasTransform.TextureRectToScreen`. The static
snapshot-based form belongs next to `TextureViewportSnapshot`; the instance form is genuinely
different (live camera, UI thread) and should stay.

**Effort** S–M. **Recommendation: follow-up issue (draft B2 below).**

---

### A3. Render-diagnostics overlay host — duplicated, already diverged

**Where**

| Concern | `TextureViewport.cs` | `PreviewControl.cs` |
| --- | --- | --- |
| `_showDiagnostics` + `RollingAverage _drawTimes = new(10)` | 279–280 | 76–77 |
| `DiagnosticsEnabled` property (incl. the identical 1 fps-repaint comment) | 334–353 | 84–103 |
| `CreateDiagnosticsTimer()` | 355–360 | 105–110 |
| `DrawOp.Render` timing wrapper (`Stopwatch` → `RenderSk` → `Add` → `DrawTimeOverlay.Draw`) | 102–113 | 2514–2527 |

**Why it matters** — already diverged in user-visible output: `TextureViewport` passes a
`"GPU"` / `"CPU"` backend label to `DrawTimeOverlay.Draw` based on `lease.GrContext`
(`TextureViewport.cs:109–110`); `PreviewControl` does not (`PreviewControl.cs:2522`). Pressing F3
therefore shows the render backend on one panel and not the other, for no reason anyone chose.
`DiagnosticsEnabled` / `CreateDiagnosticsTimer` are otherwise byte-for-byte identical including
their comments.

**Proposed extraction** — `AnimationEditor.Views/Controls/DiagnosticsOverlayHost.cs` holding
`_showDiagnostics`, the `RollingAverage`, and the repaint timer behind
`Enabled { get; set; }` + `RollingAverage? ActiveSampler`; plus move the timing wrapper into
`DrawTimeOverlay` as `DrawTimeOverlay.TimeAndDraw(lease, sampler, render)` so both `DrawOp`s call
one method and the GPU/CPU label is decided in one place.

**Effort** S. **Recommendation: do now.** Small, self-contained, and it settles the GPU/CPU
inconsistency as a side effect.

---

### A4. Pan-scrollbar host wiring — triplicated in `MainWindow`, the missing `ZoomControl.Attach` twin

**Where** — `AnimationEditor.App/MainWindow.axaml.cs`

| Pane | Suppression flag | Wire-up | `ValueChanged` handler | `Refresh…ScrollBars` |
| --- | --- | --- | --- | --- |
| Wireframe (#422) | 103 | 1156–1164 | 1167–1176 | 1183–1191 |
| PNG (#604) | 104 | 1195–1205 | 1207–1216 | 1223–1231 |
| Preview (#415) | 102 | 2329–2338 | 2357–2366 | 2378–2386 |

Shared tail: `ApplyScrollRange` (2389–2395), `OnPreviewScrollEnded` (2368–2371).

**Why it matters** — this is precisely the pattern `IZoomTarget` + `ZoomControl.Attach` already
solved for zoom, applied to nothing. The pure core (`PanScrollBar`) and the per-control range
provider (`GetScrollBarRanges()`) are both extracted; only the host glue was retyped three times.
And it has already drifted: the preview handler applies the sign inversion at the call site
(`PreviewCtrl.SetPanX(PanScrollBar.PanFromValue(value))`, 2362) while the wireframe and PNG
handlers pass the raw scrollbar value (1172, 1212) because `TextureViewport.SetPanX` applies
`PanFromValue` internally (`TextureViewport.cs:501`). Two conventions, one concept — a fourth pane
has a coin-flip chance of getting it wrong.

**Proposed extraction** — mirror the zoom design exactly:

```csharp
interface IPanScrollTarget            // implemented by TextureViewport and PreviewControl
{
    (ScrollBarRange Horizontal, ScrollBarRange Vertical) GetScrollBarRanges();
    void SetPanFromScrollValue(double h, double v);   // one convention, decided by the control
    event Action? ViewChanged;
}

static class PanScrollBinder
{
    public static void Attach(IPanScrollTarget target, ScrollBar h, ScrollBar v,
                              Action? onScrollEnd = null);
}
```

`MainWindow` then reads `PanScrollBinder.Attach(WireframeCtrl, WireframeHScroll, WireframeVScroll,
SaveCompanionFile)` ×3, deleting ~70 lines and all three suppression flags.

**Effort** M. **Recommendation: follow-up issue.** Lower priority than A1/A2 only because the
divergence is stylistic rather than a latent bug.

---

### A5. `EditorNotificationOverlay` is a verbatim clone of `MainWindow`'s toast/error banner

**Where**

- `AnimationEditor.Views/Controls/EditorNotificationOverlay.axaml.cs` — `_toastTimer` /
  `_errorBannerTimer` (15–16), `ShowToast` (53–64), `ShowErrorBanner` (66–75), `HideToast`
  (87–91), `HideErrorBanner` (93–98).
- `AnimationEditor.App/MainWindow.axaml.cs` — `_errorBannerTimer` (5324), `ShowErrorBanner`
  (5335–5346), `HideErrorBanner` (5348–5353), `_toastTimer` (5357), `InitToast` (5360–5368),
  `ShowToast` (5370–5381), `HideToast` (5383–5387).

The bodies are line-for-line identical, down to the 6 s / 8 s intervals and the
`text.TrimStart('⚠', ' ')` de-duplication of the warning glyph. The overlay's own XAML header
(`EditorNotificationOverlay.axaml:7`) says it exists *"so the browser build can host them without
duplicating"* — the extraction happened for the browser and the desktop original was left in place.

**Why it matters** — this is issue-suggestion #3 (*"host wiring already extracted once, but a
third surface still has a hand-rolled twin"*) with the twin being the *original*. Any tweak to
toast dwell time or banner wording has to be made twice, and the desktop copy is the one users
actually see.

**Proposed extraction** — host `<views:EditorNotificationOverlay/>` in `MainWindow.axaml` where the
three panels live today and delete the ~60 lines of code-behind. `ShowStatusMessage`
(5297–5320) stays in `MainWindow` — the thin bottom status bar is desktop-only chrome, not part
of the overlay.

**Effort** M — the mechanical part is small, but it touches `MainWindow.axaml` element names that
`ItemDeletedToastTests` / `StatusBarTests` / `StatusMessageTests` reach for by name, so the test
updates are the bulk of the work. **Recommendation: follow-up issue.**

---

### A6. "View &lt;file&gt; in Explorer" context menu — duplicated across the two canvas controls

**Where** — `PreviewControl.OnContextMenuOpening` (1441–1458) and
`WireframeControl.OnContextMenuOpening` (1884–1901). Identical bodies; the only difference is the
path source (`ResolveSelectedTexturePath()` vs `DetermineTexturePath()`).
`WireframeControl.cs:584` labels itself *"mirrors PreviewControl's context menu — issue #573"*.
`AnimationEditor.App/Controls/FilesPanelControl.cs:186–199, 295–300` is a third, tree-node-shaped
variant of the same idea.

**Proposed extraction** — one static in `AnimationEditor.Views/Services/`:

```csharp
static void PopulateRevealInExplorer(ContextMenu menu, string? absPath,
                                     Action<string>? showError, CancelEventArgs e);
```

Both canvas controls become a two-line handler. Leave `FilesPanelControl` alone (different menu,
multiple items, node-scoped) unless it falls out for free.

**Effort** S. **Recommendation: do now** — but only as a rider on A2, since it touches the same
two files. Not worth a standalone PR.

---

## B. Follow-up issue drafts

### B1 — `Extract the smooth wheel-zoom driver shared by TextureViewport and PreviewControl (#695)`

```markdown
## Goal

`TextureViewport` (#425) and `PreviewControl` (#451) each carry a private, hand-written copy of
the same smooth wheel-zoom state machine — target, viewport-space pivot, animating flag, 60 fps
`DispatcherTimer`, and the `Begin`/`Step`/`Settle`/`Cancel` quartet. `PreviewControl.cs:59` says
so in a comment. The copies have already diverged, and one of the divergences is a latent bug.

## Scope

1. **First, as its own test-first commit:** port `PreviewControl`'s settle-tick drift correction
   (`if (settling) _zoom = _zoomTarget;`, `PreviewControl.cs:952`) to
   `TextureViewport.StepZoomAnimation`. `git log -S "_zoom = _zoomTarget"` shows it only ever
   landed in the #451 commit. Without it the wireframe/PNG panes accumulate the same per-tick
   float drift #451 fixed, and `SetZoomPercent` (`TextureViewport.cs:754`) compounds it by going
   through a relative `ZoomToward`. Add a test that asserts an exact landing (tighter than the
   current `precision: 5` in `WireframeAnimatedZoomTests`) and that a +notch immediately after a
   settle steps to the *next* preset rather than re-targeting the current one.
2. Extract `AnimationEditor.Views/Controls/ZoomAnimator.cs` owning the five fields and the timer,
   parameterised by the host's zoom getter, its pivot-preserving apply
   (`TextureViewport.ZoomToward` / `PreviewControl.ApplyZoomTowardPivot`), and its preset list.
3. Reduce both controls to delegating one-liners. Keep the public `StepZoomAnimation` /
   `SettleZoomAnimation` / `IsZoomAnimating` / `TargetZoom` / `SimulateWheelZoomBegin` surface
   byte-identical — it is the deterministic seam the tests drive.
4. Collapse the two spellings of the non-preset zoom-out fallback (`1f / 1.25f` vs `0.8f`).

## Acceptance

- `WireframeAnimatedZoomTests` and `PreviewAnimatedZoomTests` pass unchanged (they are already
  structurally 1:1 — same four test names, same shape).
- A new test proves the wireframe pane lands exactly on its target and steps correctly on the
  following notch.
- `_zoomTimer`, `_zoomAnimating`, `_zoomTarget`, `_zoomPivotVpX`, `_zoomPivotVpY` and
  `ZoomAnimIntervalSeconds` each appear in exactly one file.

## Out of scope

- Changing the easing feel: `ZoomChase` and its time constant are untouched.
- Merging the two test files. They exercise different hosts and both should keep running.
- Anything about pan, scrollbars, or `CanvasTransform`.
```

### B2 — `Share the one-shot reveal timer host between PngPreviewControl and WireframeControl (#695)`

```markdown
## Goal

#542 originally shipped a parallel `SelectionPop` helper before being unified onto #606's
`RevealAnimation` (`49e159f`). That unified the *curve*. The *host* around it — progress field,
`DispatcherTimer` at `RevealAnimation.DefaultIntervalSeconds`, restart, step-and-stop-at-1,
`InvalidateVisual`, bounded settle loop — is still written twice, and the two copies are not even
equally testable.

## Scope

1. Extract `AnimationEditor.Views/Controls/RevealHost.cs` (Views, not Core —
   `AnimationEditor.Core` references neither Avalonia nor SkiaSharp) exposing `Progress`,
   `IsAnimating`, `Restart()`, `Step(dt)`, `Settle()`, `Stop()`.
2. Point `WireframeControl` (`_selectionRevealProgress` / `_selectionRevealTimer`, 334–335,
   513–577) at it. Keep `StepSelectionReveal`, `SettleSelectionReveal`,
   `IsSelectionRevealAnimating`, `SelectionRevealProgress` and `HandleFadeProgress` as
   delegations so `WireframeSelectionRevealTests`, `ChainSingleClickRevealTests` and
   `MultiChainSelectRevealTests` are untouched.
3. Point `PngPreviewControl` (37–38, 103–124) at it, and give it the matching public
   `StepDiffReveal` / `SettleDiffReveal` / `IsDiffRevealAnimating` seam.
4. **Add the missing tests.** The PNG diff-region bounce has no coverage today — the whole test
   tree contains no reference to `SetDiffRegions` or the PNG reveal progress. Cover: a revision
   select with `frame: true` starts the reveal; `frame: false` (slider drag) does not; settle
   lands at rest and stops the timer.
5. While in these files, hoist the duplicated texture-rect→screen wrapper.
   `WireframeControl.SnapToScreen` (253–258) and the inlined copy in
   `PngPreviewControl.DrawDiffOverlay` (161–163) should become one static next to
   `TextureViewportSnapshot`. Leave `TextureViewport.ToScreen` (1186–1190) alone — it reads the
   live camera on the UI thread, which is deliberately not the snapshot form.
6. Optional rider, same two files: fold the identical `OnContextMenuOpening`
   "View &lt;file&gt; in Explorer" bodies (`PreviewControl.cs:1441`, `WireframeControl.cs:1884`)
   into one helper.

## Acceptance

- One `DispatcherTimer`-driven reveal implementation in the tree; both controls delegate to it.
- The PNG diff reveal has tests where it previously had none.
- Existing wireframe reveal tests pass with no edits.

## Out of scope

- Merging `RevealAnimation.Scale` (multiplicative, PNG) with
  `RevealAnimation.InflationPixels` (fixed screen pixels, wireframe). They are deliberately
  different and `RevealAnimation`'s own docs explain why; only the host is being shared.
- `WireframeControl`'s auto-pan timer (638–682). Same timer shape, different contract — see the
  non-wins list in the #695 audit.
- Any change to when a reveal is triggered (`SelectedFramesIdentityChanged`,
  `ReplaySelectionReveal`).
```

---

## C. Explicit non-wins — looks duplicated, leave it alone

1. **`RevealAnimation.Scale` vs `RevealAnimation.InflationPixels`.** Same easeOutCubic, two
   outputs. Tempting to collapse into one parameterised function. Don't: one is a multiplier on a
   box's own on-screen size (PNG diff boxes) and the other is a fixed per-side pixel inflation
   (wireframe selection outline), and `RevealAnimation.cs:17–23` and `45–50` already explain that
   the multiplicative form vanishes on a small/zoomed-out box, which is exactly why #716 needed
   the pixel form. Two outputs of one curve is the correct shape.

2. **`ZoomChase.Step` vs `RevealAnimation.StepProgress`.** Both "advance an animation by `dt`",
   both driven by a 60 fps `DispatcherTimer`, so an `IAnimationStep` interface looks obvious. It
   isn't: `ZoomChase` is an exponential approach toward a *retargetable* value that terminates on
   a relative threshold, while `StepProgress` is a linear 0→1 ramp over a fixed duration that
   terminates by clamping. Nothing but ceremony is shared. Share the *timer host* (A1, A2), not
   the step contract.

3. **`WireframeControl.StepAutoPan` (#540) vs the zoom/reveal steppers.** Structurally the closest
   third `DispatcherTimer` + `Step(dt)` + test-only-driver in the codebase, and it will look like
   it belongs in A1/A2's abstraction. It doesn't: auto-pan is *continuous and condition-gated*
   (runs while a drag holds the pointer near an edge, has no target, no progress, no settle, no
   completion — it stops when the drag stops). Forcing it into a one-shot `Progress`/`Settle`
   shape would mean a permanently-0.5 progress value and a `Settle()` that means nothing. It may
   reuse a bare `Create60HzTimer` factory if one falls out; nothing more.

4. **`StepX` / `SettleX` / `IsXAnimating` public surfaces themselves.** There are five of these
   (`StepZoomAnimation`, `SettleZoomAnimation`, `StepSelectionReveal`, `SettleSelectionReveal`,
   `StepAutoPan`) and it is tempting to collapse them into one generic
   `StepAnimation(string which, float dt)`. Keep them. They are the deterministic test seam that
   lets headless tests skip the dispatcher entirely, the names document what each one drives, and
   a stringly-typed replacement would be strictly worse. The issue already calls this out; this
   audit agrees. Dedupe the *drivers behind* them, not the seam.

5. **`MainWindow.axaml.cs` (300 KB) ↔ `AnimationEditor.Browser/App.axaml.cs` (103 KB) broad
   parity.** Toolbars, tree context menus, hotkey tables, guide-toggle visibility
   (`MainWindow.axaml.cs:2349` vs `App.axaml.cs:840`) — dozens of deliberate near-copies, each
   annotated "mirrors desktop…". This is a large, tracked, intentional program
   (`docs/BROWSER_UI_PARITY_ROADMAP.md`, and the shared-control extractions
   `AnimationTreeControl` / `InspectorControl` / `ZoomControl` / `EditorNotificationOverlay` are
   how it is being retired). Out of scope for #695 except where a shared control **already
   exists** and only one host adopted it — which is A5, and A5 alone.

6. **`CanvasTransform.CenterFit` vs `CanvasTransform.FitRect`.** Both compute a fit-and-center
   camera. Different inputs (whole bitmap at a fixed 85 % vs an arbitrary rect with caller-supplied
   fraction and max zoom) and different call sites; already pure, already tested, already in Core.
   Nothing to gain.

---

## Incidental finding — stale XML doc (not fixed; audit-only task)

`WireframeControl.cs:78–81`:

```
/// <summary>Resize-handle fade-in alpha (0 = invisible, 1 = fully shown). Stays 0 until
/// <see cref="SelectionRevealProgress"/> reaches 1, so handles never overlap the
/// still-inflated frame outline.</summary>
```

This describes the behavior *before* `90c47bc Overlap handle fade-in with the tail of the shrink
instead of waiting for it`. `RevealAnimation.HandleAlpha` now starts ramping at
`HandleFadeStartFraction` (0.6), not at 1.0 — `RevealAnimation.cs:58–80` documents the new
behavior correctly, and `WireframeSelectionRevealTests` asserts it. The snapshot field's summary
is the only place left describing the old rule. One-line fix; left unapplied because this task was
scoped audit-only.
