# Phase 3: Keyframe lanes under the timeline strip

Issue: #1478 (per-frame keyframe interpolation). Phases 1 and 2 landed the file fields, the shared
`KeyframeEvaluator`, Sprite playback, and the inspector's keyframe diamond and curve picker. This phase shows
the same data on the timeline, so a chain's keyframes and easing read at a glance.

## Proposed resolution

Up to two thin lanes, Offset and Color, under the single-chain timeline's frame cells.

- Each keyframe is an amber diamond centered on the left edge of its frame; the first spills left of the strip.
- Between a keyframe and the next, the span's easing curve (the starting keyframe's, as the evaluator uses it)
  is a stroked line over a faint fill. Step draws flat, then a vertical jump. After the last keyframe, a dashed
  hold line; a looping chain's last keyframe glides back instead, as the evaluator does.
- A lane shows only when the property is in use: some keyframe's effective value differs from the previous
  keyframe's, or some span glides. Otherwise the strip keeps its current height.
- Only for one selected chain; the multi-chain group timeline shows no lanes.

## Steps

- [x] Core: `KeyframeLaneBuilder` (keyframes, spans, hold, ticks, in-use rule) with tests
- [x] Views: `KeyframeLaneControl` draws a `KeyframeLane`
- [x] `MainWindow`: lanes beside the cells' ScrollViewer, scroll sync, dock height, labels in the transport column
- [x] Headless UI tests: visibility, dock height, alignment, scroll sync, scrubbing from a lane
- [ ] Merge, then update #1478
