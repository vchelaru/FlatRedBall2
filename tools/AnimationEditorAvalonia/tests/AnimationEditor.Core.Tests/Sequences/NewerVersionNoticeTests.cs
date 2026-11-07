using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class NewerVersionNoticeTests
{
    [Fact]
    public void MessageFor_NewerFile_IsOneShortLineWithTheVersion()
    {
        var save = SequenceListSave.FromJsonString("""{ "version": 4, "sources": [], "sequences": [] }""");

        NewerVersionNotice.MessageFor(save).ShouldBe("Newer file format (v4). Unknown data is kept when you save.");
    }

    [Fact]
    public void MessageFor_CurrentVersionFile_ReturnsNull()
    {
        var save = SequenceListSave.FromJsonString("""{ "version": 1, "sources": [], "sequences": [] }""");

        NewerVersionNotice.MessageFor(save).ShouldBeNull();
    }

    [Fact]
    public void MessageFor_LegacyFileWithNoVersion_ReturnsNull()
    {
        var save = SequenceListSave.FromJsonString("""{ "sources": [], "sequences": [] }""");

        NewerVersionNotice.MessageFor(save).ShouldBeNull();
    }
}
