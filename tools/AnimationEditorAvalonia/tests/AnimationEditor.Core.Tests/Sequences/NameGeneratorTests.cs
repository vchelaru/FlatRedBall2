using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class NameGeneratorTests
{
    [Fact]
    public void Unique_FreeName_IsReturnedAsIs()
    {
        NameGenerator.Unique(new[] { "Idle" }, "Attack").ShouldBe("Attack");
    }

    [Fact]
    public void Unique_TakenName_GivesTheFirstFreeNumber_IgnoringCase()
    {
        NameGenerator.Unique(new[] { "Idle", "idle 2", "Idle 4" }, "Idle").ShouldBe("Idle 3");
    }

    [Fact]
    public void ForCopy_TakenName_GivesNumberTwo()
    {
        NameGenerator.ForCopy(new[] { "Idle" }, "Idle").ShouldBe("Idle 2");
    }

    [Fact]
    public void ForCopy_OfANumberedName_ContinuesTheNumbering()
    {
        NameGenerator.ForCopy(new[] { "Idle", "Idle 2" }, "Idle 2").ShouldBe("Idle 3");
    }

    [Fact]
    public void ForCopy_FreeName_KeepsTheName()
    {
        NameGenerator.ForCopy(new[] { "Attack" }, "Idle 2").ShouldBe("Idle 2");
    }

    [Fact]
    public void ForCopy_NameThatIsAllDigits_IsNotStrippedToNothing()
    {
        NameGenerator.ForCopy(new[] { "7" }, "7").ShouldBe("7 2");
    }
}
