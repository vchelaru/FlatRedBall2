using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Places where the .seqj tab must say and look the same as the .achx tab: toolbar tooltips, the empty Inspector, the
/// library header and its icons, the File &gt; Export parent, and the eye tooltip's look. (The library header layout
/// itself is checked with before/after screenshots too.)
/// </summary>
public class SequenceUiConsistencyScenarioTests
{
    private static string WriteSeqjWithSource(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        save.Save(path);
        return path;
    }

    private static string? Tip(Control control) => ToolTip.GetTip(control) as string;

    [AvaloniaFact]
    public async Task ExportParent_FollowsItsChildren_EnabledOnAchx_DisabledOnSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(hero);
        editor.Control<MenuItem>("MenuExport").IsEnabled.ShouldBeTrue();

        await editor.OpenAsync(boss);
        editor.Control<MenuItem>("MenuExportGif").IsEnabled.ShouldBeFalse();
        editor.Control<MenuItem>("MenuExport").IsEnabled.ShouldBeFalse("every child is greyed on a sequence tab");

        editor.ClickTab("hero.achx");
        editor.Control<MenuItem>("MenuExport").IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task PreviewToolbarTooltips_AreTheSameOnBothTabs()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Sequence"));

        var pairs = new (string Achx, string Sequence)[]
        {
            ("ShowShapesCheck", "SequenceShowShapes"),
            ("ShowOriginCheck", "SequenceShowOrigin"),
            ("ShowBoundingBoxCheck", "SequenceShowBoundingBox"),
        };
        foreach (var (achx, sequence) in pairs)
            Tip(editor.Control<ToggleButton>(sequence)).ShouldBe(Tip(editor.Control<ToggleButton>(achx)), sequence);
    }

    [AvaloniaFact]
    public async Task TheLoopTooltip_IsOneWording_InTheToolbarAndTheInspector()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Sequence"));

        string? toolbar = Tip(editor.Control<ToggleButton>("SequenceLoopToggle"));
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        string? box = Tip(inspector.GetVisualDescendants().OfType<CheckBox>().Single(check => (string?)check.Content == "Loop"));

        toolbar.ShouldNotBeNull();
        box.ShouldBe(toolbar);
        toolbar.ShouldStartWith(((string)Tip(editor.Control<ToggleButton>("LoopToggle"))!).Split(" (")[0]);
    }

    [AvaloniaFact]
    public async Task TheEmptyInspector_SaysNoSelection_InTheAchxTextStyle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("empty.seqj"));

        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        var sequenceLabel = inspector.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "No selection");
        var achxLabel = editor.Control<TextBlock>("PropNoneLabel");

        sequenceLabel.IsEffectivelyVisible.ShouldBeTrue();
        sequenceLabel.Text.ShouldBe(achxLabel.Text);
        sequenceLabel.FontSize.ShouldBe(achxLabel.FontSize);
        sequenceLabel.FontStyle.ShouldBe(achxLabel.FontStyle);
        sequenceLabel.TextAlignment.ShouldBe(achxLabel.TextAlignment);
    }

    [AvaloniaFact]
    public async Task TheLibraryTitle_IsNeverClipped_AtTheDefaultWidth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteSeqjWithSource(editor));
        var library = editor.Control<SequenceLibraryControl>("SequenceLibrary");

        var title = library.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "LIBRARY");
        title.Bounds.Width.ShouldBeGreaterThanOrEqualTo(title.DesiredSize.Width - title.Margin.Left - title.Margin.Right - 0.5, "the title got all the room it asked for");
        title.Bounds.Width.ShouldBeGreaterThan(40);
        library.AddSourceButton.TranslatePoint(new Point(library.AddSourceButton.Bounds.Width, 0), library)!.Value.X
            .ShouldBeLessThanOrEqualTo(library.Bounds.Width, "the header buttons stay inside the library");
    }

    [AvaloniaFact]
    public async Task LibraryMenus_HaveIcons_AndTheirButtonsUseTheMenuWording()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteSeqjWithSource(editor));
        var library = editor.Control<SequenceLibraryControl>("SequenceLibrary");

        var header = library.GetVisualDescendants().OfType<Grid>().First(grid => grid.ContextMenu is not null);
        var headerItems = header.ContextMenu!.Items.OfType<MenuItem>().ToList();
        headerItems.Select(item => (string?)item.Header).ShouldBe(new[] { "Open Source", "Replace Source…", "Remove Source" });
        headerItems.Where(item => item.Icon is null).ShouldBeEmpty();

        var tips = header.GetVisualDescendants().OfType<Button>().Select(Tip).Where(tip => tip is not null).ToList();
        tips.ShouldContain("Open Source");
        tips.ShouldContain("Remove Source");

        var chip = library.ChipFor("hero.achx", "Walk")!;
        chip.ContextMenu!.Items.OfType<MenuItem>().Where(item => item.Icon is null).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void TheEyeTooltip_UsesAStandardToolTipsFontAndPadding()
    {
        var real = new ToolTip { Content = "x" };
        var window = new Window { Content = new Border { Child = real } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        real.FontSize.ShouldBe(SequenceTimelineControl.EyeTooltipFontSize);
        real.Padding.ShouldBe(SequenceTimelineControl.EyeTooltipPadding);
    }
}
