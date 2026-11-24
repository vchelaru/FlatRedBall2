using AnimationEditor.Core.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class SaveTargetTests
{
    // Windows drops trailing dots and spaces from a file name, so "b.tsx." writes b.tsx.
    [Theory]
    [InlineData("b.tsx.", "achj")]
    [InlineData("b.tsx ", "achj")]
    [InlineData("b.TSX. .", "achj")]
    [InlineData("b.seqj.", "json")]
    // Its own type under such a name too: the writer and the open-tab check go by the name as typed, so both would miss it.
    [InlineData("b.achj.", "achj")]
    public void IsAnotherType_TrailingDotsAndSpaces_AreTrimmed(string path, string own) =>
        SaveTarget.IsAnotherType(path, own).ShouldBeTrue();

    [Theory]
    [InlineData("b.achj", "achj")]
    [InlineData("b.gif", "gif")]
    [InlineData("b.json", "json")]
    [InlineData("b.json.", "json")]
    [InlineData("b.", "achj")]
    [InlineData("b", "achj")]
    public void IsAnotherType_OwnOrUnknown_IsFalse(string path, string own) =>
        SaveTarget.IsAnotherType(path, own).ShouldBeFalse();
}
