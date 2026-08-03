using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class BondSystemTests
{
    [Fact]
    public void IncreaseBond_CrossesThreshold_ReturnsDialogueId()
    {
        var dialogue = new DialogueSystem();
        var bond = new BondSystem(dialogue);
        string dialogueId = "kael_bond_20";
        var character = new CharacterData
        {
            Id = "kael",
            BondConversations = new Dictionary<int, string> { { 20, dialogueId } }
        };

        var triggered = bond.IncreaseBond("kael", 20, character);

        triggered.ShouldContain(dialogueId);
    }

    [Fact]
    public void IncreaseBond_SameThresholdTwice_OnlyTriggersOnce()
    {
        var dialogue = new DialogueSystem();
        var bond = new BondSystem(dialogue);
        string dialogueId = "kael_bond_20";
        var character = new CharacterData
        {
            Id = "kael",
            BondConversations = new Dictionary<int, string> { { 20, dialogueId } }
        };

        bond.IncreaseBond("kael", 25, character);
        var second = bond.IncreaseBond("kael", 5, character);

        second.ShouldBeEmpty();
    }

    [Fact]
    public void GetBondLevel_NoIncrease_ReturnsZero()
    {
        var dialogue = new DialogueSystem();
        var bond = new BondSystem(dialogue);

        bond.GetBondLevel("kael").ShouldBe(0);
    }

    [Fact]
    public void IncreaseBond_MultipleThresholds_ReturnsAll()
    {
        var dialogue = new DialogueSystem();
        var bond = new BondSystem(dialogue);
        string dialogue20 = "kael_bond_20";
        string dialogue40 = "kael_bond_40";
        var character = new CharacterData
        {
            Id = "kael",
            BondConversations = new Dictionary<int, string>
            {
                { 20, dialogue20 },
                { 40, dialogue40 }
            }
        };

        // Jump from 0 to 50 — should trigger both 20 and 40
        var triggered = bond.IncreaseBond("kael", 50, character);

        triggered.Count.ShouldBe(2);
        triggered.ShouldContain(dialogue20);
        triggered.ShouldContain(dialogue40);
    }
}
