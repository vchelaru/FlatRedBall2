using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class CutsceneReplaySystemTests
{
    [Fact]
    public void RecordEvent_AddsToSeenEvents()
    {
        var system = new CutsceneReplaySystem();
        string eventId = "act1_intro";
        string displayName = "Ch.1 — The Rift Opens";

        system.RecordEvent(eventId, displayName);

        system.SeenEvents.Count.ShouldBe(1);
        system.SeenEvents[0].EventId.ShouldBe(eventId);
        system.SeenEvents[0].DisplayName.ShouldBe(displayName);
    }

    [Fact]
    public void RecordEvent_Duplicate_DoesNotAddTwice()
    {
        var system = new CutsceneReplaySystem();

        system.RecordEvent("act1_intro", "Ch.1 — The Rift Opens");
        system.RecordEvent("act1_intro", "Ch.1 — The Rift Opens");

        system.SeenEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void HasSeen_RecordedEvent_ReturnsTrue()
    {
        var system = new CutsceneReplaySystem();

        system.RecordEvent("act1_intro", "test");

        system.HasSeen("act1_intro").ShouldBeTrue();
        system.HasSeen("act1_elder").ShouldBeFalse();
    }
}
