using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Hostile and careless input a user can produce: negative or zero frame lengths, a zero-width
/// frame, a chain name with spaces and non-ASCII characters, the open file deleted on disk, and a
/// rename to whitespace. Off-texture pixel values and case-variant names are findings in the README.
/// </summary>
public class EdgeCaseScenarioTests
{
    [AvaloniaFact]
    public async Task ChainNameWithSpacesAndUnicode_RoundTripsThroughSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Walk Left ☃ Ünïcode");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Walk Left ☃ Ünïcode");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Name.ShouldBe("Walk Left ☃ Ünïcode");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DeletingTheOpenAchxOnDisk_TellsTheUser_AndKeepsTheDocumentEditable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        File.Delete(path);
        (await editor.WaitUntilAsync(() => editor.ToastText != null, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the editor should say the file was deleted");
        editor.ToastText!.ShouldContain("hero.achx");

        // The document is still there and an edit still works (and auto-saves it back).
        editor.Click(editor.RowButton(walk, "Add Frame"));
        walk.Frames.Count.ShouldBe(2);
        editor.ThrowIfErrorShown();
        File.Exists(path).ShouldBeTrue("auto-save recreates the file after the next edit");
    }

    [AvaloniaFact]
    public async Task FrameLength_TypingANegativeValue_IsRefused()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeFlanker("PropFrameLen", "-0.5");

        walk.Frames[0].FrameLength.ShouldBeGreaterThanOrEqualTo(0f, "a frame cannot last a negative time");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().FrameLength.ShouldBeGreaterThanOrEqualTo(0f);
    }

    [AvaloniaFact]
    public async Task FrameLength_TypingZero_IsAccepted_AndPlaybackDoesNotHang()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeFlanker("PropFrameLen", "0");
        editor.ClickRow(walk.Frames[1]);
        editor.TypeFlanker("PropFrameLen", "0");
        editor.ClickRow(walk);
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }

        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.Preview.Playback.CurrentFrameIndex.ShouldBeInRange(0, 1);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task PixelWidth_TypingZero_KeepsTheFrameAtLeastOnePixelWide()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeNumber("PropPixelW", "0");

        (walk.Frames[0].RightCoordinate - walk.Frames[0].LeftCoordinate).ShouldBeGreaterThan(0f, "a frame with no width draws nothing and cannot be grabbed again");
    }

    [AvaloniaFact]
    public async Task RenamingAChain_ToWhitespaceOnly_IsRefused()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("   ");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Walk");
        editor.ErrorBannerText.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task RectangleScale_TypingZero_IsRefusedOrKeptPositive()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();

        editor.TypeNumber("PropRectScaleX", "-3");

        rect.ScaleX.ShouldBeGreaterThan(0f, "a collision rectangle with a negative half-width is meaningless");
    }
}
