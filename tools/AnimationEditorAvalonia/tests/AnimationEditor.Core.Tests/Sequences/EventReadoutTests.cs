using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class EventReadoutTests
{
    private static EventMarker Marker(params EventOccurrence[] occurrences) => new(0, occurrences[0].Time, occurrences);

    [Fact]
    public void At_OneEvent_NamesItItsFrameAndItsTime()
    {
        var marker = Marker(new EventOccurrence("Slam", 2, 0.4, 0, false));

        var card = EventReadout.At("Attack", frameCount: 8, blockStart: 1, marker);

        card.Title.ShouldBe("Attack");
        card.Frame.ShouldBe(2);
        card.Lines.ShouldBe(new[] { "Slam", "frame 3 / 8", "at 1.40 s" });
    }

    [Fact]
    public void At_AMirroredReturnEvent_SaysItIsTheReturn()
    {
        var marker = Marker(new EventOccurrence("Bite", 0, 0.5, 0, true));

        EventReadout.At("Chomp", 3, 0, marker).Lines.ShouldContain("return");
    }

    [Fact]
    public void At_AMergedMarker_ListsEveryEventWithItsFrameAndTime()
    {
        var marker = Marker(new EventOccurrence("Hit", 1, 0.1, 0, false), new EventOccurrence("Spark", 2, 0.16, 0, false));

        var card = EventReadout.At("Combo", 6, 0, marker);

        card.Title.ShouldBe("Combo");
        card.Frame.ShouldBe(1);
        card.Lines.ShouldBe(new[] { "2 events", "Hit: frame 2, 0.10 s", "Spark: frame 3, 0.16 s" });
    }

    [Fact]
    public void At_AnUnnamedEvent_ReadsAsUnnamed()
    {
        var marker = Marker(new EventOccurrence("", 0, 0, 0, false));

        EventReadout.At("Walk", 1, 0, marker).Lines[0].ShouldBe("(unnamed)");
    }
}
