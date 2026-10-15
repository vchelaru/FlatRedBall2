# Phase 4: Shape Offset and Size keyframes

Issue: #1478 (per-frame keyframe interpolation). Phases 1 to 3 landed the file fields, the shared
`KeyframeEvaluator`, Sprite playback, the inspector diamond and curve picker, and the timeline lanes, all for
the Sprite's offset and color. This phase gives each named shape the same treatment for its Offset (X/Y) and
Size (`ScaleX`/`ScaleY`, or `Radius`). It closes #1478. Polygon points are #1484; shape color is not exposed in
the editor and is not interpolated.

## Proposed resolution

- Per shape, two independent groups, each with `Skip*Keyframe`, `*Interpolation` and `*Easing` (names as the
  frame fields; `.achj` camelCase). Offset is on every shape; Size only on rectangles and circles.
- Shapes match across frames by `Name` and kind. A glide runs to the next keyframe of the same group among the
  following frames that hold the name, and stops where the name is missing, duplicated in a frame, or changes
  kind. An unnamed or duplicated shape snaps. The first frame of a run of frames holding the name is always a
  keyframe. A looping chain's last keyframe glides to frame 0 only if frame 0 holds the name.
- `KeyframeEvaluator.EvaluateShape` (generic over frames, plus a `ShapeSave` overload for the editor);
  `IKeyframeShape` is implemented by both runtimes' `AnimationShapeFrame` and an editor adapter.
- `Sprite` evaluates each owned shape every update and applies the glided value to the entity shape.
- Editor: a diamond and curve picker above each shape's Offset and Size fields, edits reaching only shapes whose
  group is a keyframe, preview sampling, clone/undo covering the new fields.

## Steps

- [x] `.achx`/`.achj` fields on `ShapeSave`/`SizedShapeSave`, round-trip incl. unknown names
- [x] `KeyframeEvaluator.EvaluateShape` with name matching, skip runs, loop wrap; tests first
- [x] Engine and `AnimationChain.MonoGame` conversion; `Sprite` applies glided shape values
- [x] Core: `ShapeKeyframeGroup`, `ForShapes`, `SetShapeKeyframe`/`SetShapeKeyframeCurve`, sampler, clone
- [x] Inspector: `KeyframeControl` per group in the rectangle, circle and polygon panels; read-only computed fields
- [x] Preview draws glided shapes; docs and skills
- [x] Timeline lanes for a selected shape: Offset and Size lanes replace the Sprite lanes (#1489)
- [x] Polygon points as a third group (#1484): `SkipPointsKeyframe`/`PointsInterpolation`/`PointsEasing`, `KeyframeEvaluator.EvaluateShapePoints`, inspector row, Points lane
- [ ] Not done here: dragging a shape in the preview while it glides edits stored values
