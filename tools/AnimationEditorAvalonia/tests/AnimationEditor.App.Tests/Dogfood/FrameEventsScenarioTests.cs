using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.Views.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

// #1121: named frame events, authored from the frame inspector's EVENTS section.
public class FrameEventsScenarioTests
{
    [AvaloniaFact]
    public async Task AddEvent_TypeName_SavesAndDuplicateCarriesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        FrameEventsEditor events = editor.Control<FrameEventsEditor>("PropFrameEvents");
        editor.ScrollIntoView(events); // EVENTS is the last inspector section

        editor.Click(events.AddButton);
        editor.Type("Footstep");
        editor.Press(Key.Enter);

        walk.Frames[0].Events.Select(e => e.Name).ShouldBe(new[] { "Footstep" });
        editor.UndoLabels.TakeLast(2).ShouldBe(new[] { "Add Event 'Event'", "Edit Event 'Footstep'" });
        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].Events.Single().Name.ShouldBe("Footstep");

        editor.ClickRow(walk.Frames[0]); // focus back on the tree; Ctrl+D inside a text box is text editing
        editor.Press(Key.D, RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(3);
        walk.Frames[1].Events.Single().Name.ShouldBe("Footstep");
        walk.Frames[1].Events[0].ShouldNotBeSameAs(walk.Frames[0].Events[0]);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task RemoveEvent_ClickX_RemovesIt_AndCtrlZBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        FrameEventsEditor events = editor.Control<FrameEventsEditor>("PropFrameEvents");
        editor.ScrollIntoView(events); // EVENTS is the last inspector section
        editor.Click(events.AddButton);

        editor.Click(events.Rows[0].Remove);

        walk.Frames[0].Events.ShouldBeEmpty();
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames[0].Events.Single().Name.ShouldBe("Event");
        events.Rows.Count.ShouldBe(1);
    }
}
