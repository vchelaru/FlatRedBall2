using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>File &gt; Export &gt; Animated GIF: the options dialog, the save picker and the written file.</summary>
public class GifExportScenarioTests
{
    private static SKCodec OpenGif(string path) => SKCodec.Create(new SKMemoryStream(File.ReadAllBytes(path)))!;

    private static async Task ExportAsync(AnimationEditorHarness editor, string path, Func<Control, bool> dialog)
    {
        editor.Dialogs.AnswerNextEditorDialog(dialog);
        editor.Dialogs.AnswerNextSaveFile(path);

        editor.ClickMenu("MenuExportGif");

        (await editor.WaitUntilAsync(() => editor.ToastText is not null || editor.ErrorBannerText is not null, TimeSpan.FromSeconds(10)))
            .ShouldBeTrue("the export should finish with a toast or an error");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ExportGif_WithDefaultOptions_WritesOneGifFramePerAnimationFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        string gif = Path.Combine(editor.ProjectFolder, "walk.gif");

        await ExportAsync(editor, gif, _ => true);

        using SKCodec codec = OpenGif(gif);
        (codec.Info.Width, codec.Info.Height).ShouldBe((16, 16));
        codec.FrameInfo.Select(frame => frame.Duration).ShouldBe(new[] { 100, 100 });
        editor.ToastText!.ShouldContain("walk.gif");
    }

    [AvaloniaFact]
    public async Task ExportGif_DialogChoices_SetTheFrameRateAndScale()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        walk.Frames[1].RelativeX = 8;
        walk.Frames[0].OffsetInterpolation = "Linear";
        walk.Frames[1].OffsetInterpolation = "Linear"; // the chain loops, so the last frame glides back to frame 0
        editor.ClickRow(walk);
        string gif = Path.Combine(editor.ProjectFolder, "walk.gif");

        await ExportAsync(editor, gif, content =>
        {
            List<ComboBox> boxes = content.GetVisualDescendants().OfType<ComboBox>().ToList();
            boxes[0].SelectedIndex = 2; // 25 fps
            boxes[1].SelectedIndex = 1; // 2x
            content.GetVisualDescendants().OfType<RadioButton>().Single(radio => (string?)radio.Content == "Solid color").IsChecked = true;
            return true;
        });

        using SKCodec codec = OpenGif(gif);
        // 0.2s at 25fps is 5 sampled steps, every one a different eased offset except the held tail.
        codec.FrameInfo.Sum(frame => frame.Duration).ShouldBe(200);
        codec.FrameInfo.Select(frame => frame.Duration).ShouldAllBe(duration => duration == 40);
        // A 32px-wide sprite (16 at 2x) eased between x=0 and x=8 and sampled at 0, .04, .08, .12 and .16s
        // reaches at most x=6.4, i.e. 12.8 output pixels, which snaps to 3 left of centre.
        (codec.Info.Width, codec.Info.Height).ShouldBe((45, 32));
    }

    [AvaloniaFact]
    public async Task ExportGif_WithSnapCurves_KeepsOnlyTheAnimationsOwnFrames()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        walk.Frames[1].RelativeX = 8;
        editor.ClickRow(walk);
        string gif = Path.Combine(editor.ProjectFolder, "walk.gif");

        await ExportAsync(editor, gif, _ => true);

        using SKCodec codec = OpenGif(gif);
        codec.FrameCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task ExportGif_SeveralSelectedAnimations_AreStackedInOneGif()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Shadow", "sheet.png", (0, 16, 32, 8)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Shadow"), RawInputModifiers.Control);
        string gif = Path.Combine(editor.ProjectFolder, "both.gif");

        await ExportAsync(editor, gif, _ => true);

        using SKCodec codec = OpenGif(gif);
        // The 32x8 shadow is wider than the 16x16 walk frame, so the canvas holds both.
        (codec.Info.Width, codec.Info.Height).ShouldBe((32, 16));
    }

    [AvaloniaFact]
    public async Task ExportGif_CancellingTheDialog_WritesNothingAndSkipsThePicker()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Dialogs.AnswerNextEditorDialog(_ => false);

        editor.ClickMenu("MenuExportGif");
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.Dialogs.Shown.ShouldNotContain(shown => shown.StartsWith("save file"));
        Directory.GetFiles(editor.ProjectFolder, "*.gif").ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ExportGif_WithNothingSelected_ShowsAnErrorInsteadOfADialog()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);

        editor.ClickMenu("MenuExportGif");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ErrorBannerText.ShouldNotBeNull();
        editor.ErrorBannerText!.ShouldContain("Select an animation");
        editor.Dialogs.Shown.ShouldBeEmpty();
    }
}
