using System.Reflection;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Opening the Animations tab must not freeze the window. The project tree is not virtualized, so every
/// row the tab shows is built in one UI-thread block; a row that builds extra controls (a hidden Svg
/// parses its file on creation) multiplies the stall by the row count. Real report: the Cthulhu Content
/// folder (129 animation files) froze the editor 1-2 s on that click.
/// </summary>
public class ProjectTabSwitchStallScenarioTests
{
    private const int FolderCount = 10;
    private const int FilesPerFolder = 20;

    // The old row template measured ~1.9 s for these 200 rows on a developer machine and the current one
    // ~0.6 s. 1.3 s sits between them with headroom for a CI runner about twice as slow as that machine,
    // while the Svg count below is the deterministic guard if the timing ever turns noisy.
    private const double StallBudgetMs = 1300;

    [AvaloniaFact]
    public async Task ClickingTheAnimationsTab_WithTwoHundredFiles_DoesNotFreezeTheWindow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        for (int folder = 0; folder < FolderCount; folder++)
        {
            Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Group" + folder));
            for (int file = 0; file < FilesPerFolder; file++)
            {
                editor.WriteAchx($"Group{folder}/anim{file}.achx",
                    AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
            }
        }

        // The startup path: the folder loads while another sidebar tab is in front, so the tree is
        // populated but not yet built when the user clicks Animations.
        editor.Window.SidebarTabs.SelectedItem = editor.Control<TabItem>("InspectorTab");
        editor.Layout();
        MethodInfo load = typeof(MainWindow).GetMethod("LoadProjectFolderAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task<int>)load.Invoke(editor.Window, new object[] { editor.ProjectFolder })!;
        var panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        await panel.ThumbnailLoadTask;
        editor.Layout();
        panel.TreeRoots.Count.ShouldBe(FolderCount);

        using UiStallMeter meter = new UiStallMeter();
        meter.Mark("click");
        editor.Click(editor.Control<TabItem>("ProjectTab"));
        meter.Flush();
        await Task.Delay(50);

        editor.Window.SidebarTabs.SelectedItem.ShouldBe(editor.Control<TabItem>("ProjectTab"));
        meter.MaxGapMs.ShouldBeLessThan(StallBudgetMs, $"the Animations tab click froze the UI thread for {meter.MaxGapMs:F0} ms");
        // Folder rows build one glyph each; file rows with a thumbnail build no icon at all.
        panel.GetVisualDescendants().OfType<Avalonia.Svg.Skia.Svg>().Count().ShouldBeLessThanOrEqualTo(FolderCount);
    }
}
