using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class DialogueSystemTests
{
    private static DialogueSystem MakeSystem()
    {
        var system = new DialogueSystem();
        // Manually add nodes since LoadFromFile needs a real file
        // Use reflection-free approach: start dialogue from a known ID
        return system;
    }

    private static string WriteTestDialogue()
    {
        string dir = Path.Combine(Path.GetTempPath(), "riftbound_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test_dialogue.json");
        File.WriteAllText(path, """
        [
          { "id": "n1", "speaker": "Alice", "text": "Hello there.", "nextId": "n2" },
          { "id": "n2", "speaker": "Alice", "text": "How are you?", "choices": [
            { "text": "Good", "nextId": "n3a" },
            { "text": "Bad", "nextId": "n3b" }
          ]},
          { "id": "n3a", "speaker": "Alice", "text": "Glad to hear!", "nextId": null },
          { "id": "n3b", "speaker": "Alice", "text": "Sorry to hear that.", "nextId": null }
        ]
        """);
        return path;
    }

    [Fact]
    public void Advance_LinearNodes_ProgressesToNext()
    {
        string path = WriteTestDialogue();
        var system = new DialogueSystem();
        system.LoadFromFile(path);

        system.StartDialogue("n1");
        system.Current!.Text.ShouldBe("Hello there.");

        bool hasMore = system.Advance();
        hasMore.ShouldBeTrue();
        system.Current!.Id.ShouldBe("n2");
    }

    [Fact]
    public void Advance_AtEndNode_ReturnsFalseAndClearsActive()
    {
        string path = WriteTestDialogue();
        var system = new DialogueSystem();
        system.LoadFromFile(path);

        system.StartDialogue("n3a");
        system.Current!.Text.ShouldBe("Glad to hear!");

        bool hasMore = system.Advance();
        hasMore.ShouldBeFalse();
        system.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void SelectChoice_BranchesCorrectly()
    {
        string path = WriteTestDialogue();
        var system = new DialogueSystem();
        system.LoadFromFile(path);

        system.StartDialogue("n2");
        system.Current!.Choices.ShouldNotBeNull();

        // Choose "Bad" (index 1)
        bool hasMore = system.SelectChoice(1);
        hasMore.ShouldBeTrue();
        system.Current!.Id.ShouldBe("n3b");
        system.Current.Text.ShouldBe("Sorry to hear that.");
    }

    [Fact]
    public void Log_RecordsAllDisplayedLines()
    {
        string path = WriteTestDialogue();
        var system = new DialogueSystem();
        system.LoadFromFile(path);

        system.StartDialogue("n1");
        system.Advance();        // -> n2
        system.SelectChoice(0);  // -> n3a

        system.Log.Count.ShouldBe(4); // n1 text, n2 text, choice text, n3a text
        system.Log[0].ShouldContain("Hello there.");
    }
}
