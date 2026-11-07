using AnimationEditor.Core.Sequences;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class SequenceSelectionMemoryTests
{
    private static readonly string[] Names = { "Idle", "Action", "Death" };

    [Fact]
    public void RememberedName_IsRestoredToItsIndex()
    {
        var store = new Dictionary<string, string>();
        SequenceSelectionMemory.Remember(store, "c:/boss.seqj", Names, 1).ShouldBeTrue();

        store["c:/boss.seqj"].ShouldBe("Action");
        SequenceSelectionMemory.IndexToRestore(store, "c:/boss.seqj", Names).ShouldBe(1);
    }

    [Fact]
    public void RestoreIsByName_SoAReorderedFileStillFindsIt()
    {
        var store = new Dictionary<string, string> { ["f"] = "Action" };

        SequenceSelectionMemory.IndexToRestore(store, "f", new[] { "Death", "Idle", "Action" }).ShouldBe(2);
    }

    [Fact]
    public void ANameThatNoLongerExists_FallsBackToTheFirstSequence()
    {
        var store = new Dictionary<string, string> { ["f"] = "Gone" };

        SequenceSelectionMemory.IndexToRestore(store, "f", Names).ShouldBe(0);
    }

    [Fact]
    public void NothingRemembered_OpensOnTheFirst_AndAnEmptyFileOnNothing()
    {
        var store = new Dictionary<string, string>();

        SequenceSelectionMemory.IndexToRestore(store, "f", Names).ShouldBe(0);
        SequenceSelectionMemory.IndexToRestore(store, "f", new string[0]).ShouldBe(-1);
    }

    [Fact]
    public void RememberingTheSameSelectionAgain_ChangesNothing()
    {
        var store = new Dictionary<string, string>();
        SequenceSelectionMemory.Remember(store, "f", Names, 2);

        SequenceSelectionMemory.Remember(store, "f", Names, 2).ShouldBeFalse();
    }

    [Fact]
    public void RememberingAfterARename_ReplacesTheStoredName()
    {
        var store = new Dictionary<string, string> { ["f"] = "Action" };

        SequenceSelectionMemory.Remember(store, "f", new[] { "Idle", "Strike", "Death" }, 1).ShouldBeTrue();

        store["f"].ShouldBe("Strike");
        SequenceSelectionMemory.IndexToRestore(store, "f", new[] { "Idle", "Strike", "Death" }).ShouldBe(1);
    }

    [Fact]
    public void SelectingTheFirstSequence_ClearsTheEntry()
    {
        var store = new Dictionary<string, string> { ["f"] = "Action" };

        SequenceSelectionMemory.Remember(store, "f", Names, 0).ShouldBeTrue();

        store.ShouldBeEmpty();
        SequenceSelectionMemory.Remember(store, "f", Names, 0).ShouldBeFalse();
    }

    [Fact]
    public void NoSelection_LeavesTheStoreAlone()
    {
        var store = new Dictionary<string, string> { ["f"] = "Action" };

        SequenceSelectionMemory.Remember(store, "f", Names, -1).ShouldBeFalse();

        store["f"].ShouldBe("Action");
    }

    [Fact]
    public void EachFileKeepsItsOwnSelection()
    {
        var store = new Dictionary<string, string>();
        SequenceSelectionMemory.Remember(store, "a", Names, 1);
        SequenceSelectionMemory.Remember(store, "b", Names, 2);

        SequenceSelectionMemory.IndexToRestore(store, "a", Names).ShouldBe(1);
        SequenceSelectionMemory.IndexToRestore(store, "b", Names).ShouldBe(2);
    }
}
