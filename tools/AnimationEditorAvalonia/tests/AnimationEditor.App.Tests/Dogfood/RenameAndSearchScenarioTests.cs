using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Renaming chains inline (F2, double-click, the context menu, Escape, an empty name) and
/// filtering the tree with the search box.
/// </summary>
public class RenameAndSearchScenarioTests
{
    [AvaloniaFact]
    public async Task ContextMenuRename_ThenTypingAName_RenamesTheChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Rename…");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.NodeFor(walk).IsEditing.ShouldBeTrue();
        editor.Type("Stroll");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Stroll");
        editor.UndoLabels.First().ShouldContain("Stroll");
    }

    [AvaloniaFact]
    public async Task DoubleClickingAChainRow_FitsTheChainToTheWireframe_WithoutRenaming()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 256, 256);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        float zoomBefore = editor.Wireframe.CameraState.Zoom;

        // Headless hit-testing lands the double-click on the row, not its text label (see the
        // README), which is the fit-to-view gesture; the label's own double-tap renames.
        editor.DoubleClickRow(walk);
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.NodeFor(walk).IsEditing.ShouldBeFalse();
        editor.Wireframe.CameraState.Zoom.ShouldNotBe(zoomBefore, "double-clicking a chain row fits its frames in view");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task F2_ThenEscape_LeavesTheNameAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Nope");
        editor.Press(Key.Escape);

        walk.Name.ShouldBe("Walk");
        editor.NodeFor(walk).IsEditing.ShouldBeFalse();
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task F2_ThenClearingTheName_ShowsAnErrorAndKeepsTheOldName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(Key.Back);
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Walk");
        editor.ErrorBannerText.ShouldNotBeNull();
        editor.ErrorBannerText.ShouldContain("empty");
    }

    [AvaloniaFact]
    public async Task F2_ThenTypingAnotherChainsName_IsRefusedOrMadeUnique()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Run");
        editor.Press(Key.Enter);

        editor.Project.AnimationChains.Select(chain => chain.Name).Distinct().Count()
            .ShouldBe(2, "two chains must never share a name");
    }

    [AvaloniaFact]
    public async Task SearchBox_TypingPartOfAName_FiltersTheTree_AndClearingRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);

        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        TextBox box = editor.Control<TextBox>("SearchBox");
        box.IsEffectivelyVisible.ShouldBeTrue();
        editor.TypeAndEnter(box, "ru");

        editor.VisibleChainHeaders.ShouldBe(new[] { "Run" });

        editor.Click(editor.Control<Button>("SearchClearBtn"));

        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk", "Run", "Jump" });
    }
}
