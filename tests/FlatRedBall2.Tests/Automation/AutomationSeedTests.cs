using System;
using System.Collections.Generic;
using System.IO;
using FlatRedBall2.Utilities;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

public class AutomationSeedTests : IDisposable
{
    private readonly List<FlatRedBallService> _engines = new();

    // Automation mode starts a reader thread that polls its input until stopped. Without this
    // teardown every test here leaves one behind for the rest of the test-host process.
    public void Dispose()
    {
        foreach (var engine in _engines)
            engine.Shutdown();
    }

    // Empty input keeps the reader at EOF for the whole test — these cover seeding, not commands.
    private FlatRedBallService AutomatedEngine(int? seed)
    {
        var engine = new FlatRedBallService();
        _engines.Add(engine);
        engine.StartAutomationMode(seed: seed, input: new StringReader(string.Empty));
        return engine;
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

    [Fact]
    public void StartAutomationMode_DifferentSeeds_ProduceDifferentSequences()
    {
        var a = AutomatedEngine(seed: 1);
        var b = AutomatedEngine(seed: 2);

        // First int from two distinct seeds should differ — astronomically unlikely otherwise.
        a.Random.Next().ShouldNotBe(b.Random.Next());
    }

    [Fact]
    public void StartAutomationMode_NoSeed_StillDeterministic()
    {
        var a = AutomatedEngine(seed: null);
        var b = AutomatedEngine(seed: null);

        for (int i = 0; i < 16; i++)
            a.Random.Next().ShouldBe(b.Random.Next());
    }

    [Fact]
    public void StartAutomationMode_WithSeed_ProducesDeterministicRandomSequence()
    {
        var a = AutomatedEngine(seed: 1234);
        var b = AutomatedEngine(seed: 1234);

        for (int i = 0; i < 16; i++)
            a.Random.Next().ShouldBe(b.Random.Next());
    }
}
