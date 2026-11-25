using AnimationEditor.Core.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Project panel Duplicate, for the file types other than .achx and .achj.</summary>
public class DuplicateFileNameTests
{
    [Theory]
    [InlineData("seqj")]
    [InlineData("tsx")]
    public void SuggestDuplicateFileName_CopyAlreadyThere_PicksAFreeName(string extension)
    {
        string source = "Hero." + extension;

        string suggested = NewAnimationFileNaming.SuggestDuplicateFileName(source, new[] { source, "Hero2." + extension });

        suggested.ShouldBe("Hero3." + extension);
    }
}
