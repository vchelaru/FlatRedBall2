using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class SequenceAddressTests
{
    private static readonly FlatRedBall2.AnimationEditorCommon.SequenceListSave Save = SeqWorkspace.Build();

    [Fact]
    public void Sequence_ResolvesByName()
    {
        var r = SequenceAddress.ResolveSequence(Save, "Rest");

        r.Ok.ShouldBeTrue();
        r.Value!.Sequence.ShouldBe(1);
    }

    [Theory]
    [InlineData("Nope", EditErrorCode.NotFound)]
    [InlineData("", EditErrorCode.InvalidAddress)]
    [InlineData("a/b", EditErrorCode.InvalidAddress)]
    public void Sequence_Errors(string address, EditErrorCode code)
    {
        var r = SequenceAddress.ResolveSequence(Save, address);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("Run/Shadow", 1)]
    [InlineData("Run/0", 0)]
    [InlineData("Run/1", 1)]
    public void Track_ResolvesByNameOrIndex(string address, int track)
    {
        var r = SequenceAddress.ResolveTrack(Save, address);

        r.Ok.ShouldBeTrue();
        (r.Value!.Sequence, r.Value.Track).ShouldBe((0, track));
    }

    [Theory]
    [InlineData("Run/Nope", EditErrorCode.NotFound)]
    [InlineData("Run/2", EditErrorCode.IndexOutOfRange)]
    [InlineData("Nope/Body", EditErrorCode.NotFound)]
    [InlineData("Run", EditErrorCode.InvalidAddress)]
    [InlineData("Run/Body/0", EditErrorCode.InvalidAddress)]
    public void Track_Errors(string address, EditErrorCode code)
    {
        var r = SequenceAddress.ResolveTrack(Save, address);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(code);
    }

    [Fact]
    public void Block_ResolvesByIndexWithinTrack()
    {
        var r = SequenceAddress.ResolveBlock(Save, "Run/Body/1");

        r.Ok.ShouldBeTrue();
        (r.Value!.Sequence, r.Value.Track, r.Value.Block).ShouldBe((0, 0, 1));
    }

    [Theory]
    [InlineData("Run/Body/2", EditErrorCode.IndexOutOfRange)]
    [InlineData("Run/Body/x", EditErrorCode.InvalidAddress)]
    [InlineData("Run/Body", EditErrorCode.InvalidAddress)]
    public void Block_Errors(string address, EditErrorCode code)
    {
        var r = SequenceAddress.ResolveBlock(Save, address);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("*", new[] { "Run", "Rest" })]
    [InlineData("R*", new[] { "Run", "Rest" })]
    [InlineData("Re?t", new[] { "Rest" })]
    [InlineData("Run", new[] { "Run" })]
    public void ExpandSequences_Globs(string pattern, string[] expected)
    {
        var r = SequenceSelectors.ExpandSequences(Save, pattern);

        r.Targets.ShouldBe(expected);
    }

    [Fact]
    public void ExpandSequences_NoMatch_IsAnError()
    {
        var r = SequenceSelectors.ExpandSequences(Save, "Z*");

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(EditErrorCode.NoMatch);
    }

    [Theory]
    [InlineData("Run/*", new[] { "Run/Body", "Run/Shadow" })]
    [InlineData("Run/0-1", new[] { "Run/Body", "Run/Shadow" })]
    [InlineData("Run/1", new[] { "Run/Shadow" })]
    [InlineData("Run/Sh*", new[] { "Run/Shadow" })]
    [InlineData("*/Body", new[] { "Run/Body", "Rest/Body" })]
    [InlineData("R*/0", new[] { "Run/Body", "Rest/Body" })]
    public void ExpandTracks_ByGlobOrIndexSelector(string selector, string[] expected)
    {
        var r = SequenceSelectors.ExpandTracks(Save, selector);

        r.Targets.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Run/Z*", EditErrorCode.NoMatch)]
    [InlineData("Run/0-5", EditErrorCode.IndexOutOfRange)]
    [InlineData("Nope/*", EditErrorCode.NoMatch)]
    public void ExpandTracks_Errors(string selector, EditErrorCode code)
    {
        var r = SequenceSelectors.ExpandTracks(Save, selector);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(code);
    }

    [Theory]
    [InlineData("Run/Body/*", new[] { "Run/Body/0", "Run/Body/1" })]
    [InlineData("Run/*/0", new[] { "Run/Body/0", "Run/Shadow/0" })]
    [InlineData("Run/Body/0,1", new[] { "Run/Body/0", "Run/Body/1" })]
    [InlineData("Run/Body/1-1", new[] { "Run/Body/1" })]
    public void ExpandBlocks_Selectors(string selector, string[] expected)
    {
        var r = SequenceSelectors.ExpandBlocks(Save, selector);

        r.Targets.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Run/Body/0-2", EditErrorCode.IndexOutOfRange)]
    [InlineData("Run/Z*/0", EditErrorCode.NoMatch)]
    public void ExpandBlocks_Errors(string selector, EditErrorCode code)
    {
        var r = SequenceSelectors.ExpandBlocks(Save, selector);

        r.Ok.ShouldBeFalse();
        r.Code.ShouldBe(code);
    }
}

public class SequenceNameHintTests
{
    private static readonly FlatRedBall2.AnimationEditorCommon.SequenceListSave Save = SeqWorkspace.Build();

    [Fact]
    public void TypoSequence_SuggestsNearest() =>
        SequenceAddress.ResolveSequence(Save, "Rnu").Error.ShouldNotBeNull().ShouldContain("Did you mean 'Run'");

    [Fact]
    public void UnrelatedSequence_ListsValidNames() =>
        SequenceAddress.ResolveSequence(Save, "Zzzzzzzz").Error.ShouldNotBeNull().ShouldContain("Valid sequences: Run, Rest");

    [Fact]
    public void TypoTrack_SuggestsNearest() =>
        SequenceAddress.ResolveTrack(Save, "Run/Shdow").Error.ShouldNotBeNull().ShouldContain("Did you mean 'Shadow'");
}
