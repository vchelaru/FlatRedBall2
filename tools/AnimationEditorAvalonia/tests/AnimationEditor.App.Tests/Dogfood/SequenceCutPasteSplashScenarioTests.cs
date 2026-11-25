using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Reflection;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Copy, cut and paste in a .seqj when the paste is refused or the clipboard is not available.</summary>
public class SequenceCutPasteSplashScenarioTests
{
    // Idle: track "Link" plays Earth, track "Body" plays Walk. Earth: track "Body" plays Walk.
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = System.IO.Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        SequenceTrackSave Body() => new() { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" } } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Idle",
            Tracks = { new SequenceTrackSave { Name = "Link", References = [new SequenceReferenceSave { Sequence = "Earth" }] }, Body() },
        });
        save.Sequences.Add(new SequenceSave { Name = "Earth", Tracks = { Body() } });
        save.Save(path);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static void ClickHeader(AnimationEditorHarness editor, int lane) =>
        editor.ClickAt(editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name));

    private static void ClickChain(AnimationEditorHarness editor, int lane)
    {
        var viewport = Timeline(editor).Viewport;
        editor.ClickAt(editor.PointIn(Timeline(editor), viewport.XAt(0.03), viewport.LaneTop(lane) + TimelineLayout.BodyTop + 6));
    }

    private static void ClickSequence(AnimationEditorHarness editor, int row) =>
        editor.Click((Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(row)!);

    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task ChainsPastedOnATrackThatPlaysASequence_SaysSo()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        ClickChain(editor, 1);
        Timeline(editor).SelectedChains.Count.ShouldBe(1);
        await CtrlAsync(editor, Key.C);
        ClickHeader(editor, 0);

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("plays a sequence");
    }

    [AvaloniaFact]
    public async Task TracksPastedIntoTheSequenceTheyPlay_SaysSo()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        ClickHeader(editor, 0);
        await CtrlAsync(editor, Key.C);
        ClickSequence(editor, 1);
        ClickHeader(editor, 0);

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("would play itself");
    }

    public class ThrowingClipboard : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => throw new InvalidOperationException("OpenClipboard failed");
    }

    [AvaloniaFact]
    public async Task SequenceCutWhileTheClipboardIsHeldElsewhere_IsNotRemoved()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        ClickSequence(editor, 1);
        var locator = typeof(AvaloniaObject).Assembly.GetType("Avalonia.AvaloniaLocator")!;
        var mutable = locator.GetProperty("CurrentMutable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
        var original = editor.Window.Clipboard!;
        Bind(DispatchProxy.Create<IClipboard, ThrowingClipboard>());
        try
        {
            editor.Window.Clipboard.ShouldNotBeSameAs(original);
            await CtrlAsync(editor, Key.X);
        }
        finally
        {
            Bind(original);
        }

        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(["Idle", "Earth"]);

        void Bind(IClipboard clipboard)
        {
            var registration = mutable.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(IClipboard)).Invoke(mutable, null)!;
            registration.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(IClipboard)).Invoke(registration, [clipboard]);
        }
    }
}
