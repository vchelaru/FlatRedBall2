using System;
using System.Collections.Generic;
using FlatRedBall2.Automation;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

// --frb-auto-hide: an agent-driven game window is parked off-screen and at the back of the z-order
// so it does not cover the user's work, and focus goes back to the window beneath it once.
public class AutomationWindowParkerTests
{
    private sealed class FakePlacer : IWindowPlacer
    {
        public IntPtr MainWindow { get; set; } = (IntPtr)100;
        public bool Foreground { get; set; } = true;
        public IntPtr Below { get; set; } = (IntPtr)200;
        public List<string> Calls { get; } = new();

        public bool IsForeground(IntPtr window) { Calls.Add("IsForeground"); return Foreground; }
        public IntPtr WindowBelow(IntPtr window) { Calls.Add("WindowBelow"); return Below; }
        public void MoveOffScreenToBottom(IntPtr window) => Calls.Add("Move");
        public void Focus(IntPtr window) => Calls.Add($"Focus:{window}");
    }

    [Fact]
    public void EngineUpdate_WithParker_ParksWindowEvenBeforeFirstStep()
    {
        // A window waiting on its first step command must still be parked, so the tick sits ahead
        // of the step gate rather than inside a granted frame.
        var placer = new FakePlacer();
        var engine = new FlatRedBallService();
        try
        {
            engine.StartAutomationMode(input: new System.IO.StringReader(string.Empty), windowParker: new AutomationWindowParker(placer));

            engine.Update(new Microsoft.Xna.Framework.GameTime());

            placer.Calls.ShouldContain("Move");
        }
        finally { engine.Shutdown(); }
    }

    [Fact]
    public void EngineUpdate_WithoutParker_NeverTouchesTheWindow()
    {
        var placer = new FakePlacer();
        var engine = new FlatRedBallService();
        try
        {
            engine.StartAutomationMode(input: new System.IO.StringReader(string.Empty));

            engine.Update(new Microsoft.Xna.Framework.GameTime());

            placer.Calls.ShouldBeEmpty();
        }
        finally { engine.Shutdown(); }
    }

    // Shaped like MonoGame's Game.IsActive: public getter, internal setter driven by window focus.
    private sealed class FakeGame
    {
        public bool IsActive { get; internal set; }
    }

    private sealed class FakeGameWithoutIsActive { }

    // The real shape: the game is the user's subclass, and IsActive is declared on the base class.
    private class FakeGameBase
    {
        public bool IsActive { get; internal set; }
    }

    private sealed class FakeGameSubclass : FakeGameBase { }

    // MonoGame's real shape: Game.IsActive is read-only and forwards to an internal Platform object
    // whose IsActive carries the (internal) setter that window focus events drive.
    private sealed class FakePlatform
    {
        public bool IsActive { get; internal set; }
    }

    private class FakeGameWithPlatform
    {
        internal FakePlatform Platform = new();
        public bool IsActive => Platform.IsActive;
    }

    private sealed class FakeUserGame : FakeGameWithPlatform { }

    // KNI's real shape: Game.IsActive forwards to a private Strategy property, and the strategy's
    // IsActive has no setter at all -- only a private backing field that focus events write.
    private sealed class FakeStrategy
    {
        private bool _isActive;
        public bool IsActive => _isActive;
    }

    private class FakeGameWithStrategy
    {
        private FakeStrategy Strategy { get; } = new();
        public bool IsActive => Strategy.IsActive;
    }

    private sealed class FakeKniUserGame : FakeGameWithStrategy { }

    [Fact]
    public void ForceActive_StrategyIsActiveWithOnlyABackingField_MarksItActive()
    {
        var game = new FakeKniUserGame();

        AutomationWindowParker.ForceActive(game).ShouldBeTrue();

        game.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ForceActive_ReadOnlyIsActiveBackedByPlatform_MarksPlatformActive()
    {
        var game = new FakeUserGame();

        AutomationWindowParker.ForceActive(game).ShouldBeTrue();

        game.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ForceActive_GameIsSubclassOfTypeDeclaringIsActive_MarksItActive()
    {
        var game = new FakeGameSubclass();

        AutomationWindowParker.ForceActive(game).ShouldBeTrue();

        game.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ForceActive_InactiveGame_MarksItActive()
    {
        // Gum skips all Forms input while Game.IsActive is false, and handing focus back to the
        // user's window is exactly what makes it false. Injected input must not depend on focus.
        var game = new FakeGame { IsActive = false };

        AutomationWindowParker.ForceActive(game).ShouldBeTrue();

        game.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ForceActive_NoIsActiveProperty_DoesNothingAndDoesNotThrow()
    {
        AutomationWindowParker.ForceActive(new FakeGameWithoutIsActive()).ShouldBeFalse();
        AutomationWindowParker.ForceActive(null).ShouldBeFalse();
    }

    [Fact]
    public void IsHideRequested_FlagPresent_ReturnsTrue()
    {
        AutomationWindowParker.IsHideRequested(new[] { "game.exe", "--frb-auto", "--frb-auto-hide" }).ShouldBeTrue();
    }

    [Fact]
    public void IsHideRequested_OnlyAutoFlag_ReturnsFalse()
    {
        // Showing the window stays the default; hiding is strictly opt-in.
        AutomationWindowParker.IsHideRequested(new[] { "game.exe", "--frb-auto" }).ShouldBeFalse();
    }

    [Fact]
    public void Tick_NoWindowYet_DoesNothing()
    {
        var placer = new FakePlacer { MainWindow = IntPtr.Zero };
        var parker = new AutomationWindowParker(placer);

        parker.Tick();

        placer.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_FirstTickWithForegroundWindow_LooksBelowBeforeMovingThenFocusesIt()
    {
        // The window below must be read BEFORE the move: moving to the bottom changes what is below.
        var placer = new FakePlacer();
        var parker = new AutomationWindowParker(placer);

        parker.Tick();

        placer.Calls.ShouldBe(new[] { "IsForeground", "WindowBelow", "Move", "Focus:200" });
    }

    [Fact]
    public void Tick_LaterTicks_KeepMovingButNeverRefocus()
    {
        // The engine recenters the window when it resizes it, so the move repeats. Focus is handed
        // back exactly once; stealing it again every frame would fight whatever the user is doing.
        var placer = new FakePlacer();
        var parker = new AutomationWindowParker(placer);
        parker.Tick();
        placer.Calls.Clear();

        parker.Tick();
        parker.Tick();

        placer.Calls.ShouldBe(new[] { "Move", "Move" });
    }

    [Fact]
    public void Tick_WindowNotForeground_MovesWithoutTouchingFocus()
    {
        // The user already switched away; do not yank focus to whatever happens to be below.
        var placer = new FakePlacer { Foreground = false };
        var parker = new AutomationWindowParker(placer);

        parker.Tick();

        placer.Calls.ShouldBe(new[] { "IsForeground", "Move" });
    }

    [Fact]
    public void Tick_NothingBelow_MovesWithoutFocusing()
    {
        var placer = new FakePlacer { Below = IntPtr.Zero };
        var parker = new AutomationWindowParker(placer);

        parker.Tick();

        placer.Calls.ShouldBe(new[] { "IsForeground", "WindowBelow", "Move" });
    }
}
