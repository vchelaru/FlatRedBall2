using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The Animations tab's right-click menu on folder and file rows.</summary>
public class ProjectFolderMenuScenarioTests
{
    private static string[] MenuLayout(ProjectPanelControl panel) =>
        panel.ProjectTree.ContextMenu!.Items
            .Select(item => item is Separator ? "-" : (string)((MenuItem)item!).Header!)
            .ToArray();

    // A folder row's bounds include its expanded children, so aim at the header strip, not the centre.
    private static void RightClickFolderRow(AnimationEditorHarness editor, ProjectPanelControl panel, string name)
    {
        TreeViewItem row = PanelsScenarioTests.RowFor(editor, panel, name);
        editor.RightClickAt(row.TranslatePoint(new Point(row.Bounds.Width / 2, 8), editor.Window)!.Value);
    }

    private static async Task<(ProjectPanelControl Panel, string Sprites, string Props)> OpenTwoFolderProjectAsync(
        AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Sprites", "Enemies"));
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Props"));
        editor.WriteAchx("Sprites/Enemies/hero.achx", AnimationEditorHarness.Chain("Walk", "../../sheet.png", (0, 0, 16, 16)));
        editor.WriteSeqj("Props/boss.seqj", "Sequence");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        return (editor.Control<ProjectPanelControl>("ProjectPanel"),
            Path.Combine(editor.ProjectFolder, "Sprites"), Path.Combine(editor.ProjectFolder, "Props"));
    }

    [AvaloniaFact]
    public async Task FolderMenu_ListsNewItems_SeparatedFromConvertAndReveal()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (panel, _, _) = await OpenTwoFolderProjectAsync(editor);

        RightClickFolderRow(editor, panel, "Sprites");

        MenuLayout(panel).ShouldBe(new[]
        {
            "New Animation File", "New Sequence File", "-",
            "Convert Animations to .achj", "-",
            "Reveal in File Manager",
        });
    }

    [AvaloniaFact]
    public async Task FolderMenu_HoldingOnlyASequence_OffersNoConvert()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (panel, _, _) = await OpenTwoFolderProjectAsync(editor);

        RightClickFolderRow(editor, panel, "Props");

        MenuLayout(panel).ShouldBe(new[]
        {
            "New Animation File", "New Sequence File", "-",
            "Reveal in File Manager",
        });
    }

    [AvaloniaFact]
    public async Task FileMenu_SeparatesLocationItemsFromEditItemsAndDelete()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (panel, _, _) = await OpenTwoFolderProjectAsync(editor);

        editor.RightClick(PanelsScenarioTests.RowFor(editor, panel, "hero.achx"));

        MenuLayout(panel).ShouldBe(new[]
        {
            "Open Containing Folder", "Copy Full Path", "-",
            "Duplicate", "Convert to .achj", "-",
            "Delete",
        });
    }

    [AvaloniaFact]
    public async Task NewSequenceFile_OnAFolder_NamesItInline_ThenCreatesAndOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (panel, _, props) = await OpenTwoFolderProjectAsync(editor);
        string created = Path.Combine(props, "NewSequence.seqj");

        RightClickFolderRow(editor, panel, "Props");
        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, "New Sequence File");
        editor.Layout();
        AchxTreeNodeVm pending = panel.TreeRoots.Single(n => n.Name == "Props").Children.Single(n => n.IsPending);
        pending.EditText.ShouldBe("NewSequence");
        File.Exists(created).ShouldBeFalse("nothing is written until the name is committed");

        editor.Press(Key.Enter);

        (await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.Path == new FilePath(created), TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the new file opens as the active tab");
        SequenceListSave.FromFile(created).Sequences.Select(s => s.Name).ShouldBe(new[] { "Sequence" });
        editor.Control<Control>("SequenceListPane").IsVisible.ShouldBeTrue();
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task NewSequenceFile_WhenNewSequenceExists_SuggestsTheNextFreeName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (panel, _, props) = await OpenTwoFolderProjectAsync(editor);
        File.Copy(Path.Combine(props, "boss.seqj"), Path.Combine(props, "NewSequence.seqj"));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();

        RightClickFolderRow(editor, panel, "Props");
        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, "New Sequence File");
        editor.Layout();

        AchxTreeNodeVm pending = panel.TreeRoots.Single(n => n.Name == "Props").Children.Single(n => n.IsPending);
        pending.Name.ShouldBe("NewSequence2.seqj");
    }
}
