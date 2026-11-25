using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Utilities;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Random edits, undos and redos over the real commands. Undo must bring back the document and its file exactly, redo
/// must bring the edit back, and reading the file must give what memory holds. A failure names its seed and steps.
/// </summary>
[Collection("SequentialSingletons")]
public class UndoFuzzTests
{
    private const int Seeds = 8;
    private const int TsxSeeds = 20;
    private const int StepsPerSeed = 25;

    [Theory]
    [InlineData(DocType.Achx, false)]
    [InlineData(DocType.Achj, false)]
    [InlineData(DocType.Tsx, false)]
    [InlineData(DocType.Achj, true)]
    [InlineData(DocType.Tsx, true)]
    public void RandomEdits_UndoAndRedo_BringBackTheDocumentAndItsFile(DocType type, bool withTiledContent)
    {
        var failures = new List<string>();
        for (int seed = 0; seed < (type == DocType.Tsx ? TsxSeeds : Seeds); seed++)
        {
            using var dir = new TestHelpers.TempDir();
            if (new Run(type, withTiledContent, seed, dir.Path).Go() is { } failure)
                failures.Add(failure);
        }
        failures.ShouldBeEmpty();
    }

    private sealed class Run
    {
        private readonly TestServices _ctx = new();
        private readonly Random _random;
        private readonly DocType _type;
        private readonly string _path;
        private readonly List<string> _log = new();
        private readonly List<(string Model, string? File, string? Pushed)> _states = new();
        private readonly int _seed;

        public Run(DocType type, bool withTiledContent, int seed, string folder)
        {
            (_type, _seed, _random) = (type, seed, new Random(seed));
            _path = Documents.Write(folder, type, "front");
            if (withTiledContent)
            {
                string tileset = type == DocType.Tsx ? _path : Path.Combine(folder, "pushed.tsx");
                File.WriteAllText(tileset, TilesetOfTheUser);
                if (type != DocType.Tsx)
                {
                    _pushed = tileset;
                    _ctx.IoManager.AddAssociatedTiledTilesetPath(_path, tileset);
                    _ctx.AppCommands.TiledSyncFailed += (_, _) => _pushRefused = true;
                }
            }
            if (type == DocType.Tsx) _ctx.ProjectManager.LoadTsxProject(new FilePath(_path));
            else _ctx.ProjectManager.LoadAnimationChain(new FilePath(_path));
            _ctx.AppCommands.SaveCurrentAnimationChainList();
            _states.Add(State());
        }

        private AnimationChainListSave Acls => _ctx.ProjectManager.AnimationChainListSave!;

        private (string Model, string? File, string? Pushed) State()
        {
            bool saved = !_unwritable && _ctx.UndoManager.SaveState != SaveState.Failed;
            string model = Acls.ToJsonString();
            return (model, saved ? File.ReadAllText(_path) : null, saved && _pushed is not null ? File.ReadAllText(_pushed) : null);
        }

        // Two animations (one a 2x1 tile footprint) beside what Tiled itself keeps on tiles.
        private const string TilesetOfTheUser = """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="front" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <properties>
              <property name="author" value="someone"/>
             </properties>
             <image source="sheet.png" width="64" height="64"/>
             <tile id="0" class="Player">
              <properties>
               <property name="Name" value="Walk-front"/>
               <property name="speed" type="float" value="1.5"/>
              </properties>
              <objectgroup draworder="index" id="2">
               <object id="1" x="2" y="2" width="8" height="8"/>
              </objectgroup>
              <animation>
               <frame tileid="0" duration="100"/>
               <frame tileid="2" duration="250"/>
              </animation>
             </tile>
             <tile id="1">
              <properties>
               <property name="ParentId" type="int" value="0"/>
               <property name="solid" type="bool" value="true"/>
              </properties>
              <animation>
               <frame tileid="1" duration="100"/>
               <frame tileid="3" duration="250"/>
              </animation>
             </tile>
             <tile id="5">
              <properties>
               <property name="Name" value="Rock"/>
              </properties>
             </tile>
             <tile id="11">
              <animation>
               <frame tileid="8" duration="80"/>
               <frame tileid="9" duration="80"/>
               <frame tileid="8" duration="80"/>
              </animation>
             </tile>
             <wangsets>
              <wangset name="Ground" type="corner" tile="-1">
               <wangcolor name="Grass" color="#ff0000" tile="-1" probability="1"/>
               <wangtile tileid="4" wangid="0,1,0,1,0,1,0,1"/>
              </wangset>
             </wangsets>
            </tileset>
            """;

        private string? _pushed;
        private bool _unwritable;
        private bool _pushRefused; // until #2100, undo does not bring back a tile a refused push left to another animation

        public string? Go()
        {
            for (int step = 0; step < StepsPerSeed; step++)
            {
                string? failure;
                try { failure = Step(); }
                catch (Exception ex) { failure = "threw " + ex; }
                if (failure != null || step == StepsPerSeed - 1) File.SetAttributes(_path, FileAttributes.Normal);
                if (failure != null)
                    return $"[{_type} seed {_seed}] {failure}\n  " + string.Join("\n  ", _log);
            }
            return null;
        }

        private string? Step()
        {
            int at = _ctx.UndoManager.UndoHistory.Count;
            int kind = _random.Next(10);
            int special = _random.Next(20);
            if (special == 0)
            {
                _unwritable = !_unwritable;
                _log.Add(_unwritable ? "READ ONLY" : "WRITABLE");
                File.SetAttributes(_path, _unwritable ? FileAttributes.ReadOnly : FileAttributes.Normal);
                if (!_unwritable) _ctx.AppCommands.SaveCurrentAnimationChainList();
                _states[at] = State();
                return Common();
            }
            if (special == 1)
            {
                _log.Add("SWITCH TAB AND BACK");
                var tab = new TabEntry(new FilePath(_path));
                var snapshot = _ctx.UndoManager.TakeSnapshot();
                _ctx.AppCommands.CaptureTabEditorState(tab);
                _ctx.AppCommands.NewFile();
                if (!_ctx.AppCommands.TryActivateTabFromCache(tab)) return "the tab did not come back";
                _ctx.UndoManager.RestoreSnapshot(snapshot);
                return Compare("switch", _states[at]) ?? Common();
            }
            if (kind < 2 && _ctx.UndoManager.CanUndo)
            {
                _log.Add("UNDO " + _ctx.UndoManager.UndoHistory[^1].Description);
                _ctx.UndoManager.Undo();
                return Compare("undo", _states[at - 1]) ?? Common();
            }
            if (kind == 2 && _ctx.UndoManager.CanRedo)
            {
                _log.Add("REDO " + _ctx.UndoManager.RedoHistory[0].Description);
                _ctx.UndoManager.Redo();
                return Compare("redo", _states[at + 1]) ?? Common();
            }

            Select();
            if (_random.Next(3) == 0) _ctx.AppCommands.SealPendingEdits();
            var before = State();
            IUndoableCommand? top = _ctx.UndoManager.UndoHistory.LastOrDefault();
            Edit();
            int now = _ctx.UndoManager.UndoHistory.Count;
            var after = State();
            bool recorded = now != at || !ReferenceEquals(top, _ctx.UndoManager.UndoHistory.LastOrDefault());
            if (!recorded)
                return before.Model == after.Model ? Common() : "changed the document without an undo entry" + Diff(before.Model, after.Model);
            if (now > at + 1) return "two undo entries for one edit";
            _states.RemoveRange(now, _states.Count - now);
            _states.Add(after);

            _ctx.UndoManager.Undo();
            if (Compare("do, undo", _states[now - 1]) is { } undone) return undone;
            _ctx.UndoManager.Redo();
            if (Compare("do, undo, redo", _states[now]) is { } redone) return redone;
            return Common();
        }

        private string? Compare(string what, (string Model, string? File, string? Pushed) expected)
        {
            var actual = State();
            if (actual.Model != expected.Model) return $"{what}: document differs" + Diff(expected.Model, actual.Model);
            if (actual.File != expected.File && actual.File is not null && expected.File is not null) return $"{what}: file differs" + Diff(expected.File, actual.File);
            if (!_pushRefused && actual.Pushed != expected.Pushed && actual.Pushed is not null && expected.Pushed is not null) return $"{what}: pushed tileset differs" + Diff(expected.Pushed, actual.Pushed);
            return null;
        }

        private string? Common()
        {
            if (_unwritable) return null;
            if (_ctx.UndoManager.SaveState == SaveState.Failed) return "the save failed";
            if (_ctx.ProjectManager.TsxChainsNotFullySaved.Any(c => c.Frames.Count > 0)) return null;
            var other = new ProjectManager();
            if (_type == DocType.Tsx) other.LoadTsxProject(new FilePath(_path));
            else other.LoadAnimationChain(new FilePath(_path));
            var (memory, disk) = (Comparable(Acls), Comparable(other.AnimationChainListSave!));
            if (memory != disk) return "reload differs from memory" + Diff(memory, disk);
            return null;
        }

        private string Comparable(AnimationChainListSave list)
        {
            if (_type == DocType.Tsx)
                return string.Join("\n", list.AnimationChains.Where(c => c.Frames.Count > 0)
                    .Select(c => c.Name + ": " + string.Join(" ", c.Frames.Select(f =>
                        $"{f.TextureName}({f.LeftCoordinate},{f.TopCoordinate},{f.RightCoordinate},{f.BottomCoordinate};{Math.Round(f.FrameLength * 1000)})")))
                    .OrderBy(l => l, StringComparer.Ordinal));

            var copy = AnimationChainListSave.FromJsonString(list.ToJsonString());
            foreach (var frame in copy.AnimationChains.SelectMany(c => c.Frames))
            {
                if (frame.ShapesSave is not { } shapes) continue;
                if (shapes.Shapes.Count == 0) frame.ShapesSave = null;
                else if (_type == DocType.Achx) // stored in one list per type
                    shapes.SetOrder(shapes.Shapes.OfType<AARectSave>().Cast<object>()
                        .Concat(shapes.Shapes.OfType<PolygonSave>()).Concat(shapes.Shapes.OfType<CircleSave>()).ToArray());
            }
            return copy.ToJsonString();
        }

        private static string Diff(string expected, string actual)
        {
            var (e, a) = (expected.Split('\n'), actual.Split('\n'));
            int i = 0;
            while (i < e.Length && i < a.Length && e[i] == a[i]) i++;
            string At(string[] lines) => i < lines.Length ? lines[i].Trim() : "<end>";
            return $"\n    expected: {At(e)}\n    actual:   {At(a)}";
        }

        private T? Pick<T>(IReadOnlyList<T> from) => from.Count == 0 ? default : from[_random.Next(from.Count)];
        private List<T> Some<T>(IReadOnlyList<T> from) => from.OrderBy(_ => _random.Next()).Take(1 + _random.Next(3)).ToList();
        private List<AnimationFrameSave> Frames => Acls.AnimationChains.SelectMany(c => c.Frames).ToList();
        private List<ShapeSave> Shapes => Frames.SelectMany(f => f.ShapesSave?.Shapes ?? []).OfType<ShapeSave>().ToList();
        private bool Flag => _random.Next(2) == 0;
        private int Num => _random.Next(-3, 6);

        private void Select()
        {
            var state = _ctx.SelectedState;
            switch (_random.Next(5))
            {
                case 0:
                    var chains = Some(Acls.AnimationChains);
                    state.SelectedNodes = chains.Cast<object>().ToList();
                    if (chains.Count > 0) state.SelectedChain = chains[^1];
                    break;
                case 1:
                    var frames = Some(Frames);
                    state.SelectedNodes = frames.Cast<object>().ToList();
                    if (frames.Count > 0) state.SelectedFrame = frames[^1];
                    break;
                case 2 when Pick(Shapes) is { } shape:
                    state.SelectedFrame = Frames.First(f => f.ShapesSave?.Shapes.Contains(shape) == true);
                    state.SelectedNodes = [shape];
                    state.SelectShape(shape);
                    break;
            }
        }

        private void Edit()
        {
            var c = _ctx.AppCommands;
            var chain = Pick(Acls.AnimationChains);
            var frame = Pick(Frames);
            var shape = Pick(Shapes);
            var frames = Some(Frames);
            var chains = Some(Acls.AnimationChains);
            var shapes = Some(Shapes);
            var owner = frame is null ? null : Acls.AnimationChains.First(x => x.Frames.Contains(frame));
            var shapeFrame = shape is null ? null : Frames.First(f => f.ShapesSave?.Shapes.Contains(shape) == true);
            var rects = shapes.OfType<AARectSave>().ToList();
            var circles = shapes.OfType<CircleSave>().ToList();
            var polygons = shapes.OfType<PolygonSave>().ToList();
            bool tsx = _type == DocType.Tsx;
            int delta = Flag ? 1 : -1;
            string name = "N" + _random.Next(6);
            int cell = 16 * _random.Next(4), row = 16 * _random.Next(4);

            var edits = new List<(string Label, Action Run)>
            {
                ("AddNewAnimationChain", () => c.AddNewAnimationChain()),
                ("AddAnimationChainWithName " + name, () => c.AddAnimationChainWithName(name)),
                ("SortAnimationsAlphabetically", () => c.SortAnimationsAlphabetically()),
                ("HandleReorder", () => c.HandleReorder(delta)),
            };
            if (!tsx)
                edits.Add(("SetTimeMeasurementUnit", () => c.SetTimeMeasurementUnit(Flag ? TimeMeasurementUnit.Millisecond : TimeMeasurementUnit.Second)));
            if (chain is not null)
            {
                edits.AddRange(new (string, Action)[]
                {
                    ("DeleteAnimationChains " + Names(chains), () => c.DeleteAnimationChains(chains)),
                    ("RenameChain " + chain.Name + " " + name, () => c.RenameChain(chain, name)),
                    ("RenameChain " + chain.Name + " to nothing", () => c.RenameChain(chain, "")),
                    ("AddFrame " + chain.Name, () => c.AddFrame(chain)),
                    ("AddMultipleFrames " + chain.Name, () => c.AddMultipleFrames(chain, 2, Flag)),
                    ("AddFrameFromPixelBounds " + chain.Name, () => c.AddFrameFromPixelBounds(chain, "sheet.png", cell, row, cell + 16, row + 16)),
                    ("MoveChain " + chain.Name, () => c.MoveChain(chain, delta)),
                    ("MoveChainsToIndex " + Names(chains), () => c.MoveChainsToIndex(chains, _random.Next(4))),
                    ("MoveChainsRelative " + Names(chains), () => c.MoveChainsRelative(chains, delta)),
                    ("MoveChainToTop " + chain.Name, () => c.MoveChainToTop(chain)),
                    ("MoveChainToBottom " + chain.Name, () => c.MoveChainToBottom(chain)),
                    ("FlipChainHorizontally " + chain.Name, () => c.FlipChainHorizontally(chain)),
                    ("FlipChainVertically " + chain.Name, () => c.FlipChainVertically(chain)),
                    ("FlipChainDiagonally " + chain.Name, () => c.FlipChainDiagonally(chain)),
                    ("InvertFrameOrder " + chain.Name, () => c.InvertFrameOrder(chain)),
                    ("SetAllFrameLengths " + chain.Name, () => c.SetAllFrameLengths(chain, 0.1f * (1 + _random.Next(4)))),
                    ("DuplicateChains " + Names(chains), () => c.DuplicateChains(chains, (ChainFlip)_random.Next(8))),
                    ("AdjustOffsetsJustifyBottom " + chain.Name, () => c.AdjustOffsetsJustifyBottom(chain)),
                    ("AdjustOffsetsAdjustAll " + chain.Name, () => c.AdjustOffsetsAdjustAll(chain, Num, Num, Flag)),
                    ("ScaleFrameTimesProportional " + chain.Name, () => c.ScaleFrameTimesProportional(chain, 1 + _random.Next(3))),
                    ("ScaleFrameTimesSetAllSame " + chain.Name, () => c.ScaleFrameTimesSetAllSame(chain, 1 + _random.Next(3))),
                    ("SetAllFramesTextureName " + chain.Name, () => c.SetAllFramesTextureName(chain, Flag ? "other.png" : "sheet.png")),
                    ("SetChainsLocked " + Names(chains), () => c.SetChainsLocked(chains, Flag)),
                    ("SetChainsLoop " + Names(chains), () => c.SetChainsLoop(chains, Flag)),
                    ("SetChainsPingPong " + Names(chains), () => c.SetChainsPingPong(chains, Flag)),
                    ("SetChainsReverse " + Names(chains), () => c.SetChainsReverse(chains, Flag)),
                    ("PasteChains " + Names(chains), () => c.PasteChains(Clones(chains), Flag ? _random.Next(Acls.AnimationChains.Count + 1) : null)),
                    ("PasteChainsCut " + Names(chains), () => c.PasteChainsCut(chains)),
                });
                if (tsx)
                    edits.Add(("SetChainTsxOwnerTileId " + chain.Name, () => c.SetChainTsxOwnerTileId(chain, (uint)_random.Next(16))));
            }
            if (frame is not null && chain is not null)
                edits.AddRange(new (string, Action)[]
                {
                    ("DeleteFrames " + Where(frames), () => c.DeleteFrames(frames)),
                    ("DeleteSelection " + Names(chains.Take(1)) + " " + Where(frames), () => c.DeleteSelection(Flag ? chains.Take(1).ToList() : [], frames, Flag ? shapes.Cast<object>().ToList() : [])),
                    ("MoveFrame", () => c.MoveFrame(frame, owner!, delta)),
                    ("MoveFrameToTop", () => c.MoveFrameToTop(frame, owner!)),
                    ("MoveFrames " + Where(frames) + " to " + chain.Name, () => c.MoveFrames(frames.Where(owner!.Frames.Contains).DefaultIfEmpty(frame).ToList(), owner!, chain, _random.Next(4))),
                    ("SetFrameFlip", () => c.SetFrameFlip(frames, Flag ? Flag : null, Flag ? Flag : null, Flag ? Flag : null)),
                    ("DuplicateSelection frames " + Where(frames), () => c.DuplicateSelection(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = frames })),
                    ("SetFrameTextureName", () => c.SetFrameTextureName(frames, Flag ? "other.png" : "sheet.png")),
                    ("SetFrameLength", () => c.SetFrameLength(frames, 0.1f * (1 + _random.Next(4)))),
                    ("SetFrameRelative", () => c.SetFrameRelative(frames, Flag ? Num : null, Flag ? Num : null)),
                    ("SetFrameColor", () => c.SetFrameColor(frames, _random.Next(256), _random.Next(256), _random.Next(256))),
                    ("SetFrameColorOperation", () => c.SetFrameColorOperation(frames, Flag ? ColorOperation.Add : null)),
                    ("ClearFrameColorValues", () => c.ClearFrameColorValues(frames, Flag, Flag, Flag, Flag, Flag)),
                    ("SetFrameAlpha", () => c.SetFrameAlpha(frames, _random.Next(256))),
                    ("SetFrameKeyframe", () => c.SetFrameKeyframe(frames, Flag ? KeyframeGroup.Offset : KeyframeGroup.Color, Flag)),
                    ("SetFrameKeyframeCurve", () => c.SetFrameKeyframeCurve(frames, Flag ? KeyframeGroup.Offset : KeyframeGroup.Color, Pick(Enum.GetValues<KeyframeCurveChoice>()))),
                    ("SetFramePixelRegion", () => c.SetFramePixelRegion(frames, Flag ? cell : null, Flag ? row : null, !tsx && Flag ? 1 + _random.Next(16) : null, null)),
                    ("SetFramePixelRegion size", () => c.SetFramePixelRegion(frames, null, null, 16 * (1 + _random.Next(2)), 16 * (1 + _random.Next(2)))),
                    ("AddFrameEvent", () => c.AddFrameEvent(frame, name, Flag ? "data" : null)),
                    ("SetFrameEvent", () => c.SetFrameEvent(frame, 0, name, Flag ? "x" : null)),
                    ("RemoveFrameEvent", () => c.RemoveFrameEvent(frame, 0)),
                    ("AddRect", () => c.AddAxisAlignedRectangle(frame)),
                    ("AddCircle", () => c.AddCircle(frame)),
                    ("AddPolygon", () => c.AddPolygon(frame)),
                    ("PasteFrames", () => c.PasteFrames(chain, frames, Flag ? _random.Next(3) : null)),
                    ("PasteFramesCut " + Where(frames) + " to " + chain.Name, () => c.PasteFramesCut(chain, frames, Flag ? _random.Next(3) : null)),
                    ("PasteShapes", () => c.PasteShapes(frames, shapes.Count > 0 ? shapes.Cast<object>().ToList() : [new AARectSave { Name = "R", ScaleX = 1, ScaleY = 1 }])),
                });
            if (shape is not null)
                edits.AddRange(new (string, Action)[]
                {
                    ("DeleteShapes", () => c.DeleteShapes(shapes)),
                    ("DuplicateShape", () => c.DuplicateShape(shape)),
                    ("DuplicateSelection shapes", () => c.DuplicateSelection(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = shapeFrame!.ShapesSave!.Shapes.Take(2).ToList() })),
                    ("MoveShape", () => c.MoveShape(shape, shapeFrame!, delta)),
                    ("MoveShapeToTop", () => c.MoveShapeToTop(shape, shapeFrame!)),
                    ("MoveShapeToIndex", () => c.MoveShapeToIndex(shape, shapeFrame!, _random.Next(3))),
                    ("PasteShapesCut", () => c.PasteShapesCut(frame!, [shape], shapeFrame!)),
                    ("SetShapePropsBulk", () => c.SetShapePropsBulk(shapes, Flag ? name : null, Flag ? Num : null, Flag ? Num : null)),
                    ("SetShapeKeyframe", () => c.SetShapeKeyframe(shapes, Pick(Enum.GetValues<ShapeKeyframeGroup>()), Flag)),
                    ("SetShapeKeyframeCurve", () => c.SetShapeKeyframeCurve(shapes, Pick(Enum.GetValues<ShapeKeyframeGroup>()), Pick(Enum.GetValues<KeyframeCurveChoice>()))),
                    ("MatchRectanglesToFrames", () => c.MatchRectanglesToFrames(rects)),
                    ("SetRectPropsBulk", () => c.SetRectPropsBulk(rects, null, Num, null, 2, Flag ? 3 : null)),
                    ("SetCirclePropsBulk", () => c.SetCirclePropsBulk(circles, null, null, Num, 2 + _random.Next(3))),
                    ("SetPolygonPropsBulk", () => c.SetPolygonPropsBulk(polygons, name, Num, null)),
                });
            if (shape is AARectSave rect)
                edits.Add(("SetRectProps", () => c.SetRectProps(shapeFrame, rect, name, Num, Num, 1, 2)));
            if (shape is CircleSave circle)
                edits.AddRange(new (string, Action)[]
                {
                    ("SetCircleProps", () => c.SetCircleProps(shapeFrame, circle, name, Num, Num, 4)),
                    ("MatchCircleToFrame", () => c.MatchCircleToFrame(circle, shapeFrame!)),
                });
            if (shape is PolygonSave polygon)
                edits.AddRange(new (string, Action)[]
                {
                    ("MovePolygonVertex", () => c.MovePolygonVertex(polygon, _random.Next(3), Num, Num)),
                    ("InsertPolygonVertex", () => c.InsertPolygonVertex(polygon, 1, Num, Num)),
                    ("DeletePolygonVertex", () => c.DeletePolygonVertex(polygon, 1)),
                    ("FlipPolygonHorizontally", () => c.FlipPolygonHorizontally(polygon)),
                    ("SetPolygonGeometry", () => c.SetPolygonGeometry(polygon, Num, Num, Flag
                        ? [new Vector2Save { X = 0, Y = 0 }, new Vector2Save { X = 1, Y = 5 }, new Vector2Save { X = Num, Y = 9 }] : null)),
                });

            var (label, run) = edits[_random.Next(edits.Count)];
            _log.Add(label);
            run();
        }

        private List<AnimationChainSave> Clones(IEnumerable<AnimationChainSave> chains)
        {
            var used = Acls.AnimationChains.Select(x => x.Name).ToList();
            return chains.Select(x =>
            {
                var copy = AnimationCloneHelper.CloneChain(x);
                copy.Name = StringFunctions.MakeStringUnique(copy.Name, used, 2);
                used.Add(copy.Name);
                return copy;
            }).ToList();
        }

        private static string Names(IEnumerable<AnimationChainSave> chains) => "[" + string.Join(",", chains.Select(x => x.Name)) + "]";

        private string Where(IEnumerable<AnimationFrameSave> frames) => "[" + string.Join(",", frames.Select(f =>
        {
            var owner = Acls.AnimationChains.First(x => x.Frames.Contains(f));
            return owner.Name + "#" + owner.Frames.IndexOf(f);
        })) + "]";
    }
}
