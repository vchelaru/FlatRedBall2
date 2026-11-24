using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #1829: frames copied from a file in one unit and pasted into a file in another keep their duration.
/// A pasted 100 ms frame used to land in a seconds file as 100 s.
/// </summary>
public class ClipboardPayloadUnitTests
{
    private static List<AnimationFrameSave> Frames(params float[] lengths) =>
        lengths.Select(l => new AnimationFrameSave { TextureName = "a.png", FrameLength = l }).ToList();

    private static List<AnimationFrameSave> RoundTrip(List<AnimationFrameSave> frames, FrameLengthUnit source, FrameLengthUnit destination)
    {
        var text = ClipboardPayload.Serialize(frames, source);
        Assert.True(ClipboardPayload.TryDeserialize(text, destination, _ => null, out _, out var pasted, out _));
        return pasted!;
    }

    [Fact]
    public void Frames_MillisecondToSeconds_ConvertOnPaste()
    {
        var pasted = RoundTrip(Frames(100f, 250f), FrameLengthUnit.Milliseconds, FrameLengthUnit.Seconds);
        Assert.Equal(0.1f, pasted[0].FrameLength, precision: 4);
        Assert.Equal(0.25f, pasted[1].FrameLength, precision: 4);
    }

    [Fact]
    public void Frames_SecondsToMilliseconds_ConvertOnPaste()
    {
        var pasted = RoundTrip(Frames(0.1f, 0.25f), FrameLengthUnit.Seconds, FrameLengthUnit.Milliseconds);
        Assert.Equal(100f, pasted[0].FrameLength, precision: 2);
        Assert.Equal(250f, pasted[1].FrameLength, precision: 2);
    }

    [Fact]
    public void Frames_UndefinedSourceIsSeconds()
    {
        var pasted = RoundTrip(Frames(0.5f), new FrameLengthUnit(TimeMeasurementUnit.Undefined), FrameLengthUnit.Milliseconds);
        Assert.Equal(500f, pasted[0].FrameLength, precision: 2);
    }

    [Fact]
    public void Frames_SameUnit_KeepTheirLength()
    {
        Assert.Equal(250f, RoundTrip(Frames(250f), FrameLengthUnit.Milliseconds, FrameLengthUnit.Milliseconds)[0].FrameLength);
        Assert.Equal(0.25f, RoundTrip(Frames(0.25f), FrameLengthUnit.Seconds, FrameLengthUnit.Seconds)[0].FrameLength);
    }

    [Fact]
    public void Chains_ConvertEveryFrameOnPaste()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.AddRange(Frames(100f, 300f));
        var text = ClipboardPayload.Serialize(new List<AnimationChainSave> { chain }, FrameLengthUnit.Milliseconds);

        Assert.True(ClipboardPayload.TryDeserialize(text, FrameLengthUnit.Seconds, _ => null, out var chains, out _, out _));

        Assert.Equal(0.1f, chains![0].Frames[0].FrameLength, precision: 4);
        Assert.Equal(0.3f, chains[0].Frames[1].FrameLength, precision: 4);
    }

    [Fact]
    public void SerializeFromPayload_CarriesTheSourceUnit()
    {
        var payload = new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = Frames(100f) };
        var text = ClipboardPayload.SerializeFromPayload(payload, FrameLengthUnit.Milliseconds);

        Assert.Contains("Millisecond", text);
        Assert.True(ClipboardPayload.TryDeserialize(text, FrameLengthUnit.Seconds, _ => null, out _, out var pasted, out _));
        Assert.Equal(0.1f, pasted![0].FrameLength, precision: 4);
    }

    [Fact]
    public void Copying_DoesNotChangeTheSourceFrames()
    {
        var frames = Frames(100f);
        ClipboardPayload.SerializeFromPayload(
            new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = frames }, FrameLengthUnit.Milliseconds);
        Assert.Equal(100f, frames[0].FrameLength);
    }
}
