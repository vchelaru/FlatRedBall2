#if DEBUG
using System.IO;
using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

public class AutomationSeedTests
{
    [Fact]
    public void StartAutomationMode_WithSeed_ProducesDeterministicRandomSequence()
    {
        var a = new FlatRedBallService();
        var b = new FlatRedBallService();

        a.StartAutomationMode(seed: 1234, input: new StringReader(string.Empty));
        b.StartAutomationMode(seed: 1234, input: new StringReader(string.Empty));

        for (int i = 0; i < 16; i++)
            a.Random.Next().ShouldBe(b.Random.Next());
    }

    [Fact]
    public void StartAutomationMode_DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new FlatRedBallService();
        var b = new FlatRedBallService();

        a.StartAutomationMode(seed: 1, input: new StringReader(string.Empty));
        b.StartAutomationMode(seed: 2, input: new StringReader(string.Empty));

        // First int from two distinct seeds should differ — astronomically unlikely otherwise.
        a.Random.Next().ShouldNotBe(b.Random.Next());
    }

    [Fact]
    public void StartAutomationMode_NoSeed_StillDeterministic()
    {
        var a = new FlatRedBallService();
        var b = new FlatRedBallService();

        a.StartAutomationMode(input: new StringReader(string.Empty));
        b.StartAutomationMode(input: new StringReader(string.Empty));

        for (int i = 0; i < 16; i++)
            a.Random.Next().ShouldBe(b.Random.Next());
    }

    [Fact]
    public void Random_BeforeAutomation_IsNotSeeded()
    {
        // Two engines with no automation active should produce different sequences (time-based seed).
        var a = new FlatRedBallService();
        var b = new FlatRedBallService();

        // Drain a few values; at least one should differ. If all 16 match, GameRandom() is seeding deterministically by default — bug.
        bool anyDifferent = false;
        for (int i = 0; i < 16 && !anyDifferent; i++)
            if (a.Random.Next() != b.Random.Next()) anyDifferent = true;

        anyDifferent.ShouldBeTrue();
    }
}
#endif
