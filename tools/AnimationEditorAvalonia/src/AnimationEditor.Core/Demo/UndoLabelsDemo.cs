using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.Demo;

/// <summary>
/// Drives one instance of each #534 undo-label convention through real
/// <see cref="AppCommands"/> / <see cref="UndoManager.Execute"/> (not fake history rows).
/// Shared by DocScreenshots and Core.Tests — not invoked from shipping app hosts.
/// </summary>
internal static class UndoLabelsDemo
{
    public static void Run(
        AppCommands cmds,
        UndoManager undo,
        IApplicationEvents events,
        string textureName)
    {
        var walk = cmds.AddAnimationChainWithName("Walk")!;
        for (int i = 0; i < 5; i++)
            cmds.AddFrame(walk, textureName);

        var run = cmds.AddAnimationChainWithName("Run")!;
        for (int i = 0; i < 3; i++)
            cmds.AddFrame(run, textureName);

        var idle = cmds.AddAnimationChainWithName("Idle")!;
        cmds.AddFrame(idle, textureName);

        var jump = cmds.AddAnimationChainWithName("Jump")!;
        cmds.AddFrame(jump, textureName);

        var scrap = cmds.AddAnimationChainWithName("Scrap")!;
        cmds.AddFrame(scrap, textureName);

        // Drop noisy setup Add* entries so History shows only the labels under audit.
        undo.Clear();

        cmds.MoveFramesRelative(walk.Frames.Take(3).ToList(), walk, +1);
        cmds.MoveChainsRelative(new[] { walk, run }, +1);
        cmds.FlipChainHorizontally(walk);

        var bulkFrames = walk.Frames.Take(2).ToList();
        undo.Execute(new BulkFrameRegionChangedCommand(
            bulkFrames.Select(f => new BulkFrameRegionChangedCommand.FrameSnapshot(
                f,
                f.LeftCoordinate, f.TopCoordinate, f.RightCoordinate, f.BottomCoordinate,
                f.LeftCoordinate + 0.05f, f.TopCoordinate + 0.05f,
                f.RightCoordinate + 0.05f, f.BottomCoordinate + 0.05f)).ToList(),
            cmds, events));

        undo.Execute(new BulkFrameRegionChangedCommand(
            new[]
            {
                new BulkFrameRegionChangedCommand.FrameSnapshot(
                    walk.Frames[0],
                    walk.Frames[0].LeftCoordinate, walk.Frames[0].TopCoordinate,
                    walk.Frames[0].RightCoordinate, walk.Frames[0].BottomCoordinate,
                    walk.Frames[0].LeftCoordinate + 0.02f, walk.Frames[0].TopCoordinate,
                    walk.Frames[0].RightCoordinate + 0.02f, walk.Frames[0].BottomCoordinate),
                new BulkFrameRegionChangedCommand.FrameSnapshot(
                    walk.Frames[1],
                    walk.Frames[1].LeftCoordinate, walk.Frames[1].TopCoordinate,
                    walk.Frames[1].RightCoordinate, walk.Frames[1].BottomCoordinate,
                    walk.Frames[1].LeftCoordinate + 0.08f, walk.Frames[1].TopCoordinate + 0.03f,
                    walk.Frames[1].RightCoordinate + 0.08f, walk.Frames[1].BottomCoordinate + 0.03f),
            },
            cmds, events));

        cmds.MoveChain(idle, +1);
        cmds.MoveChainToTop(jump);
        cmds.MoveFrame(walk.Frames[0], walk, +1);
        cmds.MoveFrameToTop(walk.Frames[^1], walk);
        cmds.MoveFrames(new[] { walk.Frames[0] }, walk, walk, insertIndex: 2);
        cmds.InvertFrameOrder(run);

        cmds.DuplicateChains(new[] { idle });
        var pasteChain = new AnimationChainSave { Name = "PastedWalk" };
        pasteChain.Frames.Add(AnimationCloneHelper.CloneFrame(walk.Frames[0]));
        cmds.PasteChains(new[] { pasteChain });

        var shapeFrame = walk.Frames[0];
        cmds.AddAxisAlignedRectangle(shapeFrame);
        cmds.AddCircle(shapeFrame);

        var rect = shapeFrame.ShapesSave!.AARectSaves.First();
        var circle = shapeFrame.ShapesSave!.CircleSaves.First();
        rect.Name = "Hitbox";
        circle.Name = "Hurtbox";

        cmds.AddAxisAlignedRectangle(shapeFrame);
        shapeFrame.ShapesSave!.AARectSaves.Last().Name = "Other";

        cmds.MoveShape(rect, shapeFrame, +1);
        cmds.DuplicateShape(rect);

        var pasteCircle = (CircleSave)AnimationCloneHelper.CloneShape(circle)!;
        pasteCircle.Name = "HurtboxPaste";
        cmds.PasteShapes(shapeFrame, Array.Empty<AARectSave>(), new[] { pasteCircle });

        var cutSource = shapeFrame.ShapesSave!.AARectSaves.First(r => r.Name == "Other");
        var cutClone = (AARectSave)AnimationCloneHelper.CloneShape(cutSource)!;
        cmds.PasteShapesCut(shapeFrame, new[] { cutClone }, [], new[] { (object)cutSource }, shapeFrame);

        var cutFrameSrc = scrap.Frames[0];
        cmds.PasteFramesCut(walk, new[] { AnimationCloneHelper.CloneFrame(cutFrameSrc) },
            insertIndex: null, sourcesToRemove: new[] { cutFrameSrc });

        var cutAnimClone = AnimationCloneHelper.CloneChain(scrap);
        cutAnimClone.Name = "Scrap";
        cmds.PasteChainsCut(new[] { cutAnimClone }, new[] { scrap });

        var doomed = cmds.AddAnimationChainWithName("Doomed")!;
        cmds.AddFrame(doomed, textureName);
        cmds.DeleteFrames(new List<AnimationFrameSave> { doomed.Frames[0] });
        cmds.DeleteAnimationChains(new List<AnimationChainSave> { doomed });
    }
}
