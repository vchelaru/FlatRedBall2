using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// <see cref="EditorClipboard.IsEditorEnvelope"/> is true for every text the editor's own Copy and Cut put on the system
/// clipboard, and false for ordinary text, so a name box can refuse the first and take the second.
/// </summary>
public class EditorClipboardTests
{
    private static SequenceTrackSave Track(string name)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" });
        return track;
    }

    [Fact]
    public void ACopiedSequence_IsAnEnvelope()
    {
        EditorClipboard.IsEditorEnvelope(SequenceClipboard.Write(new SequenceSave { Name = "Idle" }, @"C:\game")).ShouldBeTrue();
    }

    [Fact]
    public void SeveralCopiedSequences_AreAnEnvelope()
    {
        var text = SequenceClipboard.Write(new[] { new SequenceSave { Name = "A" }, new SequenceSave { Name = "B" } }, @"C:\game");

        EditorClipboard.IsEditorEnvelope(text).ShouldBeTrue();
    }

    [Fact]
    public void CopiedTracks_AreAnEnvelope()
    {
        EditorClipboard.IsEditorEnvelope(TimelineClipboard.WriteTracks(new[] { Track("Body") }, @"C:\game")).ShouldBeTrue();
    }

    [Fact]
    public void CopiedChains_AreAnEnvelope()
    {
        var track = Track("Body");
        var text = TimelineClipboard.WriteChains(new[] { new CopiedChain(track.Chains[0], track, 0) }, @"C:\game");

        EditorClipboard.IsEditorEnvelope(text).ShouldBeTrue();
    }

    [Fact]
    public void ADamagedEnvelope_IsStillAnEnvelope()
    {
        EditorClipboard.IsEditorEnvelope("{ \"kind\": \"frb2-animation-editor-sequence\" }").ShouldBeTrue();
    }

    [Fact]
    public void CopiedAchxChains_AreAnEnvelope()
    {
        var text = ClipboardPayload.Serialize(new List<AnimationChainSave> { new() { Name = "Walk" } });

        EditorClipboard.IsEditorEnvelope(text).ShouldBeTrue();
    }

    [Fact]
    public void CopiedAchxFrames_AreAnEnvelope()
    {
        var text = ClipboardPayload.Serialize(new List<AnimationFrameSave> { new() { TextureName = "a.png" } });

        EditorClipboard.IsEditorEnvelope(text).ShouldBeTrue();
    }

    [Fact]
    public void CopiedAchxShapes_AreAnEnvelope()
    {
        EditorClipboard.IsEditorEnvelope(ClipboardPayload.Serialize(new AARectSave { Name = "Box" })).ShouldBeTrue();
        EditorClipboard.IsEditorEnvelope(ClipboardPayload.Serialize(new CircleSave { Name = "Ball" })).ShouldBeTrue();
        EditorClipboard.IsEditorEnvelope(ClipboardPayload.SerializeShapes(new object[] { new AARectSave(), new CircleSave() })).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("Walk Left")]
    [InlineData("C:\\game\\hero.achx")]
    [InlineData("note: remember this")]
    [InlineData("{")]
    [InlineData("{ \"kind\": \"something-else\" }")]
    [InlineData("[1, 2, 3]")]
    public void OrdinaryText_IsNotAnEnvelope(string? text)
    {
        EditorClipboard.IsEditorEnvelope(text).ShouldBeFalse();
    }
}
