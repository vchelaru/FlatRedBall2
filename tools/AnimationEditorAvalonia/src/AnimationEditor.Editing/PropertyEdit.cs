using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Channels to change on a frame's color; a null channel is left as it is.</summary>
public readonly record struct FrameColorEdit(int? Red = null, int? Green = null, int? Blue = null, int? Alpha = null, ColorOperation? Operation = null);

/// <summary>Sets chain flags, frame properties and the file's time unit (#1811).</summary>
public static class PropertyEdit
{
    public static EditResult SetChainLoop(IAppCommands commands, AnimationChainSave chain, bool value) => throw new NotImplementedException();
    public static EditResult SetChainPingPong(IAppCommands commands, AnimationChainSave chain, bool value) => throw new NotImplementedException();
    public static EditResult SetChainReverse(IAppCommands commands, AnimationChainSave chain, bool value) => throw new NotImplementedException();
    public static EditResult SetChainLocked(IAppCommands commands, AnimationChainSave chain, bool value) => throw new NotImplementedException();
    public static EditResult SetFrameLength(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, float length) => throw new NotImplementedException();
    public static EditResult SetFrameRegion(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, FramePixelRegion region) => throw new NotImplementedException();
    public static EditResult SetFrameOffset(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, float? x, float? y) => throw new NotImplementedException();
    public static EditResult SetFrameColor(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, FrameColorEdit edit) => throw new NotImplementedException();
    public static EditResult SetFrameFlip(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, bool? flipH, bool? flipV, bool? flipDiagonal) => throw new NotImplementedException();
    public static EditResult SetTimeUnit(IProjectManager projectManager, IAppCommands commands, TimeMeasurementUnit unit) => throw new NotImplementedException();
}
