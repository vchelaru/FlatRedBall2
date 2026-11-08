using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// An in-place rename in the ANIMATIONS tree (a chain or a shape) that is still open when the user does anything
/// else commits the typed name, the way a click-away on another row does. Escape still cancels, and a refused name
/// behaves as it does on a click-away: the banner, the old name.
/// </summary>
public class AchxRenameLeaveScenarioTests
{
    // hero.achx: Walk (two frames), Run (one frame). The first Walk frame holds a rectangle.
    private static string WriteFixture(AnimationEditorHarness editor, string name = "hero.achx")
    {
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        return editor.WriteAchx(name, walk, AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
    }

    private static List<string> SavedChainNames(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(chain => chain.Name).ToList();

    private static string SavedRectName(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Single().Name;

    private static void BeginChainRename(AnimationEditorHarness editor, string chainName, string newName)
    {
        AnimationChainSave chain = editor.ChainNamed(chainName);
        editor.ClickRow(chain);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.NodeFor(chain).IsEditing.ShouldBeTrue();
        editor.Type(newName);
    }

    private static void BeginRectRename(AnimationEditorHarness editor, string newName)
    {
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        editor.ClickRow(rect);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.NodeFor(rect).IsEditing.ShouldBeTrue();
        editor.Type(newName);
    }

    private static void MouseButtonClick(AnimationEditorHarness editor, MouseButton button)
    {
        var point = new Point(300, 300);
        editor.Window.MouseDown(point, button);
        editor.Window.MouseUp(point, button);
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginChainRename(editor, "Run", "Sprint");
        editor.ClickTab("other.achx");

        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
        SavedChainNames(other).ShouldBe(new[] { "Walk", "Run" });
        editor.ErrorBannerText.ShouldBeNull();
        editor.ClickTab("hero.achx");
        editor.UndoLabels.ShouldHaveSingleItem().ShouldContain("Sprint");
    }

    [AvaloniaFact]
    public async Task SwitchingToASequenceTab_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string boss = editor.WriteSeqj("boss.seqj", "One", "Two");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(boss);
        editor.ClickTab("hero.achx");

        BeginChainRename(editor, "Run", "Sprint");
        editor.ClickTab("boss.seqj");

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileRenamingAShape_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginRectRename(editor, "Hurt");
        editor.ClickTab("other.achx");

        SavedRectName(hero).ShouldBe("Hurt");
    }

    [AvaloniaFact]
    public async Task OpeningAnotherFile_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        await editor.OpenAsync(other);

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingTheTab_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.CloseTab("hero.achx");

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.Window.Close();

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WhileRenamingAShape_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginRectRename(editor, "Hurt");
        editor.Window.Close();

        SavedRectName(hero).ShouldBe("Hurt");
    }

    [AvaloniaFact]
    public async Task CtrlZ_WhileRenaming_CommitsFirst_SoUndoTakesTheRenameBack_AndCtrlYBringsItAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        BeginChainRename(editor, "Walk", "Stroll");
        editor.Press(Key.Enter);
        BeginChainRename(editor, "Run", "Sprint");

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Stroll", "Run" });
        editor.UndoLabels.ShouldHaveSingleItem().ShouldContain("Stroll");
        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Stroll", "Sprint" });
    }

    [AvaloniaFact]
    public async Task EditUndoFromTheMenu_WhileRenaming_CommitsFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        BeginChainRename(editor, "Walk", "Stroll");
        editor.Press(Key.Enter);
        BeginChainRename(editor, "Run", "Sprint");

        editor.ClickMenu("MenuUndo");

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Stroll", "Run" });
        editor.ClickMenu("MenuRedo");
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Stroll", "Sprint" });
    }

    [AvaloniaFact]
    public async Task CtrlS_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.Press(Key.S, RawInputModifiers.Control);

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task MouseBack_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        BeginChainRename(editor, "Run", "Sprint");

        MouseButtonClick(editor, MouseButton.XButton1);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
        editor.ErrorBannerText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task AltLeft_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        BeginChainRename(editor, "Run", "Sprint");

        editor.Press(Key.Left, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task PressingOnTheWireframe_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.ClickAt(editor.PointIn(editor.Wireframe, 5, 5));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task PressingOnThePreview_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.ClickAt(editor.PointIn(editor.Preview, 5, 5));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task PressingOnTheEmptyTreeSpace_WhileRenamingAChain_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginChainRename(editor, "Run", "Sprint");
        editor.ClickAt(editor.PointIn(editor.AnimTree, 20, editor.AnimTree.Bounds.Height - 5));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ARefusedName_OnLeavingByTabSwitch_ShowsTheBanner_AndKeepsTheOldName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginChainRename(editor, "Run", "x");
        editor.Press(Key.Back);
        editor.ClickTab("other.achx");

        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("empty");
        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Run" });
        editor.ClickTab("hero.achx");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Escape_StillCancels_ThenLeavingChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginChainRename(editor, "Run", "Sprint");
        editor.Press(Key.Escape);
        editor.ClickTab("other.achx");
        editor.ClickTab("hero.achx");

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Run" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    // The inspector's name boxes commit on Enter or focus loss, like the tree's box.
    private static void BeginInspectorChainName(AnimationEditorHarness editor, string chainName, string newName)
    {
        editor.ClickRow(editor.ChainNamed(chainName));
        TextBox box = editor.Control<TextBox>("PropChainName");
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(newName);
    }

    private static void BeginInspectorRectName(AnimationEditorHarness editor, string newName)
    {
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.ClickRow(walk.Frames[0].ShapesSave!.AARectSaves.Single());
        TextBox box = editor.Control<TextBox>("PropRectName");
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(newName);
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginInspectorChainName(editor, "Run", "Sprint");
        editor.ClickTab("other.achx");

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
        SavedChainNames(other).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileTypingTheInspectorRectName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");

        BeginInspectorRectName(editor, "Hurt");
        editor.ClickTab("other.achx");

        SavedRectName(hero).ShouldBe("Hurt");
        SavedRectName(other).ShouldBe("Hit");
    }

    [AvaloniaFact]
    public async Task CtrlS_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginInspectorChainName(editor, "Run", "Sprint");
        editor.Press(Key.S, RawInputModifiers.Control);

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        BeginInspectorChainName(editor, "Run", "Sprint");
        editor.Window.Close();

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingTheTab_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);

        BeginInspectorChainName(editor, "Run", "Sprint");
        editor.CloseTab("hero.achx");

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task MouseBack_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        BeginInspectorChainName(editor, "Run", "Sprint");

        MouseButtonClick(editor, MouseButton.XButton1);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
    }

    [AvaloniaFact]
    public async Task OpeningAnotherFile_WhileTypingTheInspectorChainName_CommitsTheName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(hero);

        BeginInspectorChainName(editor, "Run", "Sprint");
        await editor.OpenAsync(other);

        SavedChainNames(hero).ShouldBe(new[] { "Walk", "Sprint" });
        SavedChainNames(other).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task EditUndoFromTheMenu_WhileTypingTheInspectorChainName_CommitsFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        BeginChainRename(editor, "Walk", "Stroll");
        editor.Press(Key.Enter);
        BeginInspectorChainName(editor, "Run", "Sprint");

        editor.ClickMenu("MenuUndo");

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Stroll", "Run" });
    }
}
