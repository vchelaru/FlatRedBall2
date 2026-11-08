using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The harness's single click stays a single click. Two presses at one spot inside the OS double-click
/// window read as a double-click, which the tree takes without selecting the row; a test that deletes a
/// row and clicks the one that moved up into its place lands exactly there.
/// </summary>
public class HarnessClickScenarioTests
{
    [AvaloniaFact]
    public async Task ClickAt_TwiceOnTheSameSpot_IsTwoSingleClicks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(hero);
        List<int> clickCounts = new List<int>();
        editor.Window.AddHandler(InputElement.PointerPressedEvent,
            (object? _, PointerPressedEventArgs args) => clickCounts.Add(args.ClickCount), RoutingStrategies.Tunnel, handledEventsToo: true);
        Point spot = editor.RowHeaderPoint(editor.ChainNamed("Walk"));

        editor.ClickAt(spot);
        editor.ClickAt(spot);

        clickCounts.ShouldBe(new[] { 1, 1 });
    }
}
