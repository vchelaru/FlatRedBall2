using AnimationEditor.Core.IO;
using Shouldly;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Naming for "New Sequence File" on a Project-tree folder row.</summary>
public class NewSequenceFileNamingTests
{
    [Fact]
    public void SuggestSequenceFileName_NothingTaken_IsNewSequence()
    {
        NewAnimationFileNaming.SuggestSequenceFileName(new[] { "hero.achx" }).ShouldBe("NewSequence.seqj");
    }

    [Fact]
    public void SuggestSequenceFileName_NameTaken_AddsTheFirstFreeNumber()
    {
        NewAnimationFileNaming.SuggestSequenceFileName(new[] { "NewSequence.seqj", "NewSequence2.seqj" })
            .ShouldBe("NewSequence3.seqj");
    }

    [Fact]
    public void ResolveSequence_AddsTheExtension_AndAcceptsATypedOne()
    {
        NewAnimationFileNaming.ResolveSequence("Boss", Array.Empty<string>()).FileName.ShouldBe("Boss.seqj");
        NewAnimationFileNaming.ResolveSequence("Boss.seqj", Array.Empty<string>()).FileName.ShouldBe("Boss.seqj");
    }

    [Fact]
    public void ResolveSequence_TakenName_IsRejectedRatherThanSuffixed()
    {
        var result = NewAnimationFileNaming.ResolveSequence("boss", new[] { "Boss.seqj" });

        result.FileName.ShouldBeNull();
        result.Error.ShouldBe("\"boss\" already exists in this folder.");
    }

    [Fact]
    public void ResolveSequence_AnAnimationFileWithTheSameStem_IsNotACollision()
    {
        NewAnimationFileNaming.ResolveSequence("Boss", new[] { "Boss.achx" }).FileName.ShouldBe("Boss.seqj");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".seqj")]
    public void ResolveSequence_EmptyName_AsksForOne(string typed)
    {
        NewAnimationFileNaming.ResolveSequence(typed, Array.Empty<string>()).Error.ShouldBe("Enter a name.");
    }

    [Fact]
    public void ResolveSequence_InvalidCharacters_AreRejected()
    {
        NewAnimationFileNaming.ResolveSequence("a/b", Array.Empty<string>()).Error
            .ShouldBe("Name contains invalid characters.");
    }
}
