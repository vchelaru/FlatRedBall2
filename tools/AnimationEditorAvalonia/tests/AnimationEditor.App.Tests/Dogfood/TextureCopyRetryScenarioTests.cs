using System.Reflection;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Retry of a failed texture copy belongs to the document, frames and lock state it was asked for (#2177).
/// The banner outlives a tab switch by its 8 s, or for as long as it is hovered.
/// </summary>
public class TextureCopyRetryScenarioTests : IDisposable
{
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "AeRetry", Guid.NewGuid().ToString("N"));

    public TextureCopyRetryScenarioTests() => Directory.CreateDirectory(_outside);

    public void Dispose() => Directory.Delete(_outside, recursive: true);

    private string WritePngOutside(AnimationEditorHarness editor)
    {
        string png = Path.Combine(_outside, "held.png");
        File.Move(editor.WritePng("held.png", 64, 64), png);
        return png;
    }

    private static async Task<(string Hero, string Other)> OpenTabsAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);
        return (hero, other);
    }

    private static Task<bool> Drop(AnimationEditorHarness editor, AnimationChainSave? chain, AnimationFrameSave? frame, string png, bool ctrl = false) =>
        (Task<bool>)typeof(MainWindow).GetMethod("HandlePngDropAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(editor.Window, [chain, frame, png, ctrl])!;

    private static void AnswerCopy(AnimationEditorHarness editor) => editor.Dialogs.AnswerNextEditorDialog(content =>
    {
        ClickFirstButton(content);
        return true;
    });

    private static void ClickFirstButton(Control content) =>
        content.GetVisualDescendants().OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task ReloadAsync(AnimationEditorHarness editor, string hero)
    {
        File.WriteAllText(hero, File.ReadAllText(hero).Replace("Walk", "Stroll"));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains[0].Name == "Stroll", TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    private static async Task PressRetryAsync(AnimationEditorHarness editor)
    {
        editor.Layout();
        editor.Click(editor.Notifications.ErrorBannerRetryBtn);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
    }

    [AvaloniaTheory]
    [InlineData("tab", "hero.achx")]
    [InlineData("png tab", "hero.achx")]
    [InlineData("close", "hero.achx")]
    [InlineData("reload", "hero.achx")]
    [InlineData("delete", "gone")]
    [InlineData("undo", "gone")]
    [InlineData("delete animation", "its animation is gone")]
    [InlineData("lock", "'Walk' is locked")]
    public async Task RetryOfADrop_WhenWhatItWasForChanged_OnlySaysWhy(string since, string reason)
    {
        using var editor = new AnimationEditorHarness();
        string png = WritePngOutside(editor);
        var (hero, other) = await OpenTabsAsync(editor);
        var walk = editor.ChainNamed("Walk");
        if (since == "undo") editor.Services.AppCommands.AddFrame(walk);
        var frame = walk.Frames[^1];
        AnswerCopy(editor);
        using (new FileStream(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            (await Drop(editor, walk, since == "delete animation" ? null : frame, png)).ShouldBeFalse();
        editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Could not copy: ");

        switch (since)
        {
            case "tab": editor.ClickTab("other.achx"); break;
            case "png tab": await editor.OpenPngTabAsync(Path.Combine(editor.ProjectFolder, "sheet.png")); break;
            case "close": editor.CloseTab("hero.achx"); break;
            case "reload": await ReloadAsync(editor, hero); break;
            case "delete": editor.Services.AppCommands.DeleteFrames([frame]); break;
            case "delete animation": editor.Services.AppCommands.DeleteAnimationChains([walk]); break;
            case "undo": editor.UndoManager.Undo(); break;
            case "lock": editor.Services.AppCommands.SetChainLocked(walk, true); break;
        }
        editor.Layout();
        string heroBefore = File.ReadAllText(hero), otherBefore = File.ReadAllText(other);
        var undoBefore = editor.UndoLabels;
        await PressRetryAsync(editor);

        frame.TextureName.ShouldBe("sheet.png");
        editor.UndoLabels.ShouldBe(undoBefore);
        File.ReadAllText(hero).ShouldBe(heroBefore);
        File.ReadAllText(other).ShouldBe(otherBefore);
        File.Exists(Path.Combine(editor.ProjectFolder, "held.png")).ShouldBeFalse();
        editor.ProblemText.ShouldNotBeNull().ShouldContain(reason);
    }

    [AvaloniaTheory]
    [InlineData("row")]
    [InlineData("ctrl row")]
    [InlineData("browse")]
    [InlineData("asks first")]
    public async Task Retry_InAnotherTab_OnlySaysWhy_ForEveryWayIn(string way)
    {
        using var editor = new AnimationEditorHarness();
        string png = WritePngOutside(editor);
        var (hero, other) = await OpenTabsAsync(editor);
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        // The same bytes beside the .achx: in use, they cannot be compared, which fails before the question (#2076).
        string inside = Path.Combine(editor.ProjectFolder, "held.png");
        if (way == "asks first") File.Copy(png, inside);
        else AnswerCopy(editor);
        using (new FileStream(way == "asks first" ? inside : png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (way == "browse") await editor.Window.ApplyPickedTextureAsync(png);
            else await Drop(editor, walk, way == "asks first" ? walk.Frames[0] : null, png, ctrl: way == "ctrl row");
        }
        editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Could not copy: ");
        int asked = editor.Dialogs.Shown.Count;

        editor.ClickTab("other.achx");
        await PressRetryAsync(editor);

        editor.Dialogs.Shown.Count.ShouldBe(asked);
        walk.Frames.ShouldHaveSingleItem().TextureName.ShouldBe("sheet.png");
        editor.UndoLabels.ShouldBeEmpty();
        File.Exists(inside).ShouldBe(way == "asks first");
        editor.ProblemText.ShouldNotBeNull().ShouldContain("hero.achx");
    }

    [AvaloniaFact]
    public async Task Retry_AfterGoingToAnotherTabAndBack_Applies()
    {
        using var editor = new AnimationEditorHarness();
        string png = WritePngOutside(editor);
        var (hero, _) = await OpenTabsAsync(editor);
        var walk = editor.ChainNamed("Walk");
        AnswerCopy(editor);
        using (new FileStream(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Drop(editor, walk, walk.Frames[0], png);

        editor.ClickTab("other.achx");
        editor.ClickTab("hero.achx");
        await PressRetryAsync(editor);

        editor.ChainNamed("Walk").Frames[0].TextureName.ShouldBe("held.png");
        AnimationEditorHarness.ReadSaved(hero).AnimationChains[0].Frames[0].TextureName.ShouldBe("held.png");
    }

    [AvaloniaTheory]
    [InlineData("hero2.achx")]
    [InlineData("moved/hero.achx")]
    public async Task Retry_AfterSaveAs_AppliesToTheSameDocument_NextToItsNewFile(string savedAs)
    {
        using var editor = new AnimationEditorHarness();
        string png = WritePngOutside(editor);
        await OpenTabsAsync(editor);
        var walk = editor.ChainNamed("Walk");
        string path = Path.Combine(editor.ProjectFolder, savedAs);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AnswerCopy(editor);
        using (new FileStream(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Drop(editor, walk, walk.Frames[0], png);

        editor.Dialogs.AnswerNextSaveFile(path);
        editor.ClickMenu("MenuSaveAs");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        await PressRetryAsync(editor);

        walk.Frames[0].TextureName.ShouldBe("held.png");
        File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "held.png")).ShouldBeTrue();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheDocumentReloadsUnderTheCopyQuestion_TheAnswerCopiesNothing_AndSaysWhy(bool dropped)
    {
        using var editor = new AnimationEditorHarness();
        typeof(MainWindow).GetField("_dialogHost", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(editor.Window, new WindowEditorDialogHost(editor.Window));
        string png = WritePngOutside(editor);
        var (hero, _) = await OpenTabsAsync(editor);
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Task asking = dropped ? Drop(editor, walk, walk.Frames[0], png) : editor.Window.ApplyPickedTextureAsync(png);

        await ReloadAsync(editor, hero);
        ClickFirstButton(editor.Window.OwnedWindows.Single());
        await asking;

        File.Exists(Path.Combine(editor.ProjectFolder, "held.png")).ShouldBeFalse();
        editor.UndoLabels.ShouldBeEmpty();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("hero.achx");
    }
}
