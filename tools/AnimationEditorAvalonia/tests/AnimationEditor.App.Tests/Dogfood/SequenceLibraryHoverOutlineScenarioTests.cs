using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A library chip shows its red outline exactly while the pointer is over it (#1655): whatever the chain's frame count,
/// after a click, a menu, a drag, a collapse or play-all, and moving between chips in any order.
/// </summary>
public class SequenceLibraryHoverOutlineScenarioTests
{
    private static readonly (string Source, string Chain)[] ChipKeys =
    {
        ("hero.achx", "Walk"), ("hero.achx", "Idle"), ("hero.achx", "Run"), ("foe.achx", "Bite"),
    };

    private static string WriteLibrary(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16), (16, 16, 16, 16)));
        editor.WriteAchx("foe.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        SequenceListSave save = new SequenceListSave();
        save.Sources.AddRange(new[] { "hero.achx", "foe.achx" });
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        save.Save(path);
        return path;
    }

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    private static LibraryChipView Chip(AnimationEditorHarness editor, string chain) =>
        Library(editor).ChipFor(ChipKeys.First(key => key.Chain == chain).Source, chain)!;

    private static bool ShowsOutline(SequenceLibraryControl library, LibraryChipView chip)
    {
        library.TryFindResource("Accent", library.ActualThemeVariant, out object? accent);
        Color color = accent is Color c ? c : ((ISolidColorBrush)accent!).Color;
        return chip.BorderBrush is ISolidColorBrush brush && brush.Color == color;
    }

    /// <summary>Every chip's outline equals "the pointer is over it", and the playing chips are the over ones plus play-all's.</summary>
    private static void AssertOutlineFollowsPointer(AnimationEditorHarness editor, string step)
    {
        SequenceLibraryControl library = Library(editor);
        foreach (var (source, chain) in ChipKeys)
        {
            LibraryChipView chip = library.ChipFor(source, chain)!;
            ShowsOutline(library, chip).ShouldBe(chip.IsPointerOver, $"{step}: {chain} outline vs pointer over");
        }
    }

    [AvaloniaFact]
    public async Task OutlineFollowsThePointer_ForOneFrameAndManyFrameChips_AcrossEveryGesture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLibrary(editor));
        SequenceLibraryControl library = Library(editor);
        Point away = editor.CenterOf(editor.Control<Button>("AddSequenceButton"));
        Point Center(string chain) => editor.CenterOf(Chip(editor, chain));
        void Step(string name, Action action) { action(); AssertOutlineFollowsPointer(editor, name); }

        Step("hover Walk", () => editor.Hover(Center("Walk")));
        Step("hover Run", () => editor.Hover(Center("Run")));
        Step("hover Walk again", () => editor.Hover(Center("Walk")));
        Step("hover the one-frame Idle", () => editor.Hover(Center("Idle")));
        ShowsOutline(library, Chip(editor, "Idle")).ShouldBeTrue();
        Step("hover Bite", () => editor.Hover(Center("Bite")));
        Step("jump Walk to Bite", () => { editor.Hover(Center("Walk")); editor.Window.MouseMove(Center("Bite")); editor.Layout(); });
        Step("leave", () => editor.Hover(away));

        Step("click Idle", () => editor.ClickAt(Center("Idle")));
        Step("hover Run after a click", () => editor.Hover(Center("Run")));
        Step("right-click Idle", () => editor.RightClickAt(Center("Idle")));
        Step("menu closed with Escape", () =>
        {
            editor.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            editor.Layout();
        });
        Step("hover Run after the menu", () => editor.Hover(Center("Run")));

        Step("drag Idle away", () => editor.DragHold(Center("Idle"), away));
        Step("drag Idle over Run", () => editor.DragMove(Center("Run")));
        Step("release over Run", () => editor.DragRelease(Center("Run")));
        Step("hover Idle after the drag", () => editor.Hover(Center("Idle")));
        Step("leave after the drag", () => editor.Hover(away));

        Step("hover Idle, then collapse hero", () => { editor.Hover(Center("Idle")); library.ToggleSource("hero.achx"); editor.Layout(); });
        Step("expand hero", () => { library.ToggleSource("hero.achx"); editor.Layout(); });
        Step("hover Idle after expand", () => editor.Hover(Center("Idle")));

        Step("play-all on", () => editor.Click(library.GetVisualDescendants().OfType<ToggleButton>().Single()));
        Step("play-all, hover Idle", () => editor.Hover(Center("Idle")));
        Step("play-all, hover Walk", () => editor.Hover(Center("Walk")));
        Step("play-all, leave", () => editor.Hover(away));
    }

    [AvaloniaFact]
    public async Task HoveringAOneFrameChip_ShowsTheOutline_ButStillDoesNotPlay()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteLibrary(editor));
        LibraryChipView idle = Chip(editor, "Idle");

        editor.Hover(editor.CenterOf(idle));

        ShowsOutline(Library(editor), idle).ShouldBeTrue();
        idle.IsPlaying.ShouldBeFalse();
        Library(editor).IsPlaybackTimerRunning.ShouldBeFalse();
    }
}
