using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class NewerVersionNoticeTests
{
    [Fact]
    public void MessageFor_NewerFile_NamesTheFileAndBothVersions()
    {
        var save = SequenceListSave.FromJsonString("""{ "version": 4, "sources": [], "sequences": [] }""");

        string? message = NewerVersionNotice.MessageFor("boss.seqj", save);

        message.ShouldNotBeNull();
        message.ShouldContain("boss.seqj");
        message.ShouldContain("format version 4");
        message.ShouldContain("understands up to 1");
        message.ShouldContain("kept when you save");
    }

    [Fact]
    public void MessageFor_CurrentVersionFile_ReturnsNull()
    {
        var save = SequenceListSave.FromJsonString("""{ "version": 1, "sources": [], "sequences": [] }""");

        NewerVersionNotice.MessageFor("boss.seqj", save).ShouldBeNull();
    }

    [Fact]
    public void MessageFor_LegacyFileWithNoVersion_ReturnsNull()
    {
        var save = SequenceListSave.FromJsonString("""{ "sources": [], "sequences": [] }""");

        NewerVersionNotice.MessageFor("boss.seqj", save).ShouldBeNull();
    }
}
