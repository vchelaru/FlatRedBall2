using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>File &gt; Export &gt; Animated WebP: the options dialog, the save picker and the written file.</summary>
public class WebpExportScenarioTests
{
    private static SKCodec OpenWebp(string path) => SKCodec.Create(new SKMemoryStream(File.ReadAllBytes(path)))!;

    private static async Task ExportAsync(AnimationEditorHarness editor, string path, Func<Control, bool> dialog)
    {
        editor.Dialogs.AnswerNextEditorDialog(dialog);
        editor.Dialogs.AnswerNextSaveFile(path);

        editor.ClickMenu("MenuExportWebp");

        (await editor.WaitUntilAsync(() => editor.ToastText is not null || editor.ErrorBannerText is not null, TimeSpan.FromSeconds(10)))
            .ShouldBeTrue("the export should finish with a toast or an error");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ExportWebp_WithDefaultOptions_WritesAnAnimatedWebpWithEachFramesDuration()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // The blob makes the two frames look different; WebP merges identical neighbours into one.
        editor.WritePngWithBlob("sheet.png", 64, 64, (0, 0, 16, 16));
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        string webp = Path.Combine(editor.ProjectFolder, "walk.webp");

        await ExportAsync(editor, webp, _ => true);

        using SKCodec codec = OpenWebp(webp);
        codec.EncodedFormat.ShouldBe(SKEncodedImageFormat.Webp);
        (codec.Info.Width, codec.Info.Height).ShouldBe((16, 16));
        codec.FrameInfo.Select(frame => frame.Duration).ShouldBe(new[] { 100, 100 });
        editor.ToastText!.ShouldContain("walk.webp");
    }

    [AvaloniaFact]
    public async Task ExportWebp_Dialog_OffersSixtyFpsAndOnlyEnablesQualityWhenNotLossless()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // The blob makes the two frames look different; WebP merges identical neighbours into one.
        editor.WritePngWithBlob("sheet.png", 64, 64, (0, 0, 16, 16));
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        string webp = Path.Combine(editor.ProjectFolder, "walk.webp");

        await ExportAsync(editor, webp, content =>
        {
            ComboBox rates = content.GetVisualDescendants().OfType<ComboBox>().First();
            rates.ItemsSource!.Cast<string>().First().ShouldBe("60 fps");
            rates.SelectedIndex.ShouldBe(0, "WebP defaults to its fastest rate");

            Slider quality = content.GetVisualDescendants().OfType<Slider>().Single();
            CheckBox lossless = content.GetVisualDescendants().OfType<CheckBox>().Single();
            lossless.IsChecked.ShouldBe(true);
            quality.IsEnabled.ShouldBeFalse("quality only applies to lossy output");

            lossless.IsChecked = false;
            quality.IsEnabled.ShouldBeTrue();
            quality.Value = 40;
            return true;
        });

        using SKCodec codec = OpenWebp(webp);
        codec.FrameCount.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task ExportWebp_Dialog_DoesNotShowTheGifOnlyFrameRateWarning()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<TextBlock>()
                .ShouldNotContain(text => text.Text != null && text.Text.Contains("60 isn't offered"));
            return false;
        });

        editor.ClickMenu("MenuExportWebp");
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.Dialogs.Shown.ShouldNotBeEmpty();
    }
}
