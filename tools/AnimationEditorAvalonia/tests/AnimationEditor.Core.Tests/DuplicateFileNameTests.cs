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

    /// <summary>Files of one stem share its .aeproperties, whichever of the three formats they are in.</summary>
    [Theory]
    [InlineData("achx", "tsx")]
    [InlineData("achj", "tsx")]
    [InlineData("tsx", "achx")]
    [InlineData("tsx", "achj")]
    [InlineData("achx", "achj")]
    public void SuggestDuplicateFileName_StemOfAnotherFormatWithACompanion_IsTaken(string extension, string other)
    {
        NewAnimationFileNaming.SuggestDuplicateFileName("Hero." + extension, new[] { "Hero." + extension, "Hero2." + other })
            .ShouldBe("Hero3." + extension);
    }

    [Theory]
    [InlineData("achx", "seqj")]
    [InlineData("seqj", "achx")]
    [InlineData("seqj", "tsx")]
    public void SuggestDuplicateFileName_SequenceFileOfTheStem_IsNotInTheWay(string extension, string other)
    {
        NewAnimationFileNaming.SuggestDuplicateFileName("Hero." + extension, new[] { "Hero." + extension, "Hero2." + other })
            .ShouldBe("Hero2." + extension);
    }
}
