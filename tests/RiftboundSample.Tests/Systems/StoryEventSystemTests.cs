using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class StoryEventSystemTests
{
    private static string WriteTestEvents()
    {
        string dir = Path.Combine(Path.GetTempPath(), "riftbound_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test_events.json");
        File.WriteAllText(path, """
        [
          {
            "Id": "evt1",
            "TriggerType": "map_enter",
            "TriggerValue": "town_a",
            "RequiredFlags": [],
            "SetFlags": ["evt1_done"],
            "DialogueFile": ""
          },
          {
            "Id": "evt2",
            "TriggerType": "map_enter",
            "TriggerValue": "town_a",
            "RequiredFlags": ["evt1_done"],
            "SetFlags": ["evt2_done"],
            "DialogueFile": "",
            "RecruitCharacter": "alice"
          },
          {
            "Id": "evt3",
            "TriggerType": "boss_defeat",
            "TriggerValue": "boss_x",
            "RequiredFlags": ["evt2_done"],
            "SetFlags": ["act1_complete"],
            "DialogueFile": ""
          }
        ]
        """);
        return path;
    }

    [Fact]
    public void CheckTrigger_MatchingEvent_ReturnsEvent()
    {
        var system = new StoryEventSystem();
        system.LoadFromFile(WriteTestEvents());
        var flags = new HashSet<string>();

        var evt = system.CheckTrigger("map_enter", "town_a", flags);

        evt.ShouldNotBeNull();
        evt.Id.ShouldBe("evt1");
    }

    [Fact]
    public void CheckTrigger_MissingRequiredFlag_ReturnsNull()
    {
        var system = new StoryEventSystem();
        system.LoadFromFile(WriteTestEvents());
        var flags = new HashSet<string>();

        // evt2 requires "evt1_done" flag
        system.CompleteEvent("evt1"); // mark evt1 done so it won't match
        var evt = system.CheckTrigger("map_enter", "town_a", flags);

        // evt2 requires evt1_done but flags is empty
        evt.ShouldBeNull();
    }

    [Fact]
    public void CheckTrigger_WithRequiredFlag_ReturnsEvent()
    {
        var system = new StoryEventSystem();
        system.LoadFromFile(WriteTestEvents());
        var flags = new HashSet<string> { "evt1_done" };

        system.CompleteEvent("evt1");
        var evt = system.CheckTrigger("map_enter", "town_a", flags);

        evt.ShouldNotBeNull();
        evt.Id.ShouldBe("evt2");
        evt.RecruitCharacter.ShouldBe("alice");
    }

    [Fact]
    public void CompleteEvent_PreventsRefire()
    {
        var system = new StoryEventSystem();
        system.LoadFromFile(WriteTestEvents());
        var flags = new HashSet<string>();

        system.CompleteEvent("evt1");
        var evt = system.CheckTrigger("map_enter", "town_a", flags);

        // evt1 completed, evt2 needs flag — nothing should match
        evt.ShouldBeNull();
    }

    [Fact]
    public void RestoreCompleted_PreventsPreviouslyCompletedEvents()
    {
        var system = new StoryEventSystem();
        system.LoadFromFile(WriteTestEvents());
        var flags = new HashSet<string>();

        system.RestoreCompleted(["evt1"]);
        var evt = system.CheckTrigger("map_enter", "town_a", flags);

        evt.ShouldBeNull();
    }
}
