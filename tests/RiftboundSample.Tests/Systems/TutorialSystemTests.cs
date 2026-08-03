using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class TutorialSystemTests
{
    [Fact]
    public void TryGetTutorial_FirstTime_ReturnsText()
    {
        var flags = new HashSet<string>();
        var system = new TutorialSystem(flags);
        string tutorialId = TutorialSystem.BattleBasics;

        string? text = system.TryGetTutorial(tutorialId);

        text.ShouldNotBeNull();
        text.ShouldContain("Active Time Battle");
    }

    [Fact]
    public void TryGetTutorial_SecondTime_ReturnsNull()
    {
        var flags = new HashSet<string>();
        var system = new TutorialSystem(flags);
        string tutorialId = TutorialSystem.BattleBasics;

        system.TryGetTutorial(tutorialId);
        string? second = system.TryGetTutorial(tutorialId);

        second.ShouldBeNull();
    }

    [Fact]
    public void TryGetTutorial_AlreadyInFlags_ReturnsNull()
    {
        string tutorialId = TutorialSystem.PetCare;
        var flags = new HashSet<string> { tutorialId };
        var system = new TutorialSystem(flags);

        string? text = system.TryGetTutorial(tutorialId);

        text.ShouldBeNull();
    }
}
