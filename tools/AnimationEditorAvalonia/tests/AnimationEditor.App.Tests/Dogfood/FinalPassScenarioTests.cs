using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Interactions between recent changes that hold: a pending cut with Convert and an outside change, odd numbers in a file, a typed field and its frame.</summary>
public class FinalPassScenarioTests
{
    private const string Text = """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>Pixel</CoordinateType>
          <AnimationChain>
            <Name>Walk</Name>
            <Frame>
              <TextureName>sheet.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate>
              <RightCoordinate>16</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>16</BottomCoordinate>
              <RelativeX>1</RelativeX>
            </Frame>
            <Frame>
              <TextureName>sheet.png</TextureName>
              <FrameLength>0.2</FrameLength>
              <LeftCoordinate>16</LeftCoordinate>
              <RightCoordinate>32</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>16</BottomCoordinate>
              <RelativeX>2</RelativeX>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    [AvaloniaTheory]
    [InlineData("<FrameLength>0.1<", "<FrameLength>NaN<")]
    [InlineData("<FrameLength>0.1<", "<FrameLength>1E30<")]
    [InlineData("<FrameLength>0.1<", "<FrameLength>-1<")]
    [InlineData("<RelativeX>1<", "<RelativeX>NaN<")]
    [InlineData("<RelativeX>1<", "<RelativeX>1E30<")]
    [InlineData("<RightCoordinate>16<", "<RightCoordinate>NaN<")]
    [InlineData("<RightCoordinate>16<", "<RightCoordinate>1E30<")]
    public async Task AFileWithAnOddNumber_OpensAndItsFramesCanBeSelectedAndPlayed(string from, string to)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = Path.Combine(editor.ProjectFolder, "hero.achx");
        File.WriteAllText(hero, Text.Replace(from, to));
        await editor.OpenAsync(hero);
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[1], RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.ThrowIfErrorShown();
        editor.ProblemText.ShouldBeNullOrEmpty();
    }

    // The cut's source file changed outside before the paste: neither the change nor the item is lost.
    [AvaloniaFact]
    public async Task CutIntoAnotherTab_WhoseSourceChangedOnDisk_KeepsTheChangeAndTheItem()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string source = editor.WriteAchx("source.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)), AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 0, 16, 16)));
        string target = editor.WriteAchx("target.achx", AnimationEditorHarness.Chain("Target", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(target);
        await editor.OpenAsync(source);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.ClickTab("target.achx");
        File.WriteAllText(source, File.ReadAllText(source).Replace("Idle", "Outside"));
        editor.ClickRow(editor.ChainNamed("Target"));
        editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        string? problem = editor.ProblemText;
        editor.ClickTab("source.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        AnimationEditorHarness.ReadSaved(target).AnimationChains.Select(c => c.Name).ShouldContain("Walk");
        AnimationEditorHarness.ReadSaved(source).AnimationChains.Select(c => c.Name).ShouldContain("Outside", problem);
    }

    // Convert while a cut is pending: the paste must not bring the .achx back, and loses nothing.
    [AvaloniaFact]
    public async Task ConvertingTheSourceOfAPendingCut_ThenPaste_DoesNotBringTheAchxBack()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string source = editor.WriteAchx("source.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)), AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 0, 16, 16)));
        string target = editor.WriteAchx("target.achx", AnimationEditorHarness.Chain("Target", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(target);
        await editor.OpenAsync(source);
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Dialogs.AnswerNextEditorDialog(_ => true);
        _ = editor.Window.ConvertProjectFileToAchjAsync("source.achx");
        editor.WaitUntil(() => editor.Dialogs.Shown.Count > 0 && !editor.Window.IsConverting, TimeSpan.FromSeconds(10)).ShouldBeTrue();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.ClickRow(editor.ChainNamed("Target"));
        editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        File.Exists(source).ShouldBeFalse();
        AnimationEditorHarness.ReadSaved(Path.ChangeExtension(source, ".achj")).AnimationChains.Select(c => c.Name).ShouldBe(["Walk", "Idle"]);
        AnimationEditorHarness.ReadSaved(target).AnimationChains.Select(c => c.Name).ShouldBe(["Target", "Walk"]);
    }

    // A Length typed for one frame must never end up on another one.
    [AvaloniaTheory]
    [InlineData("duplicate")]
    [InlineData("canvas")]
    [InlineData("delete")]
    [InlineData("move")]
    [InlineData("addframe")]
    public async Task ALengthTypedForAFrame_IsNotCommittedToAnotherOne(string way)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);
        var walk = editor.ChainNamed("Walk");
        foreach (var f in walk.Frames) f.FrameLength.ShouldBe(walk.Frames[0].FrameLength);
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);
        var typedFor = editor.Services.SelectedState.SelectedFrame!;
        var box = editor.Control<Control>("PropFrameLen").GetVisualDescendants().OfType<TextBox>().First();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type("0.5");
        switch (way)
        {
            case "duplicate": editor.Press(Key.D, RawInputModifiers.Control); break;
            case "delete": editor.Press(Key.Delete); break;
            case "move": editor.Press(Key.Down, RawInputModifiers.Alt); break;
            case "addframe": editor.Click(editor.RowButton(walk, "Add Frame")); break;
            case "canvas": editor.ClickAt(editor.WireframePointAt(40, 8), RawInputModifiers.Control); break;
        }
        editor.Wait(TimeSpan.FromMilliseconds(100));
        string focus = editor.Window.FocusManager?.GetFocusedElement()?.GetType().Name ?? "none";
        string info = $"focus {focus}, same frame {ReferenceEquals(typedFor, editor.Services.SelectedState.SelectedFrame)}, text {box.Text}, frames {walk.Frames.Count}";
        editor.ClickTab("other.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        walk.Frames.Where(f => f != typedFor && f.FrameLength == 0.5f).Count().ShouldBe(0, info);
        (walk.Frames.Contains(typedFor) ? typedFor.FrameLength : 0.5f).ShouldBe(0.5f, info);
    }

    [AvaloniaTheory]
    [InlineData("PropFrameLen", "0.5")]
    [InlineData("PropRed", "7")]
    public async Task DuplicatingTheOpenFile_FromTheProjectTabsMenu_WhileAFieldIsTyped_CopiesWhatIsTyped(string field, string text)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.Window.SidebarTabs.SelectedItem = editor.Control<TabItem>("InspectorTab");
        editor.Layout();
        editor.Expand(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Walk").Frames[0]);
        var box = editor.Control<Control>(field).GetVisualDescendants().OfType<TextBox>().First();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(text);
        var before = Directory.GetFiles(editor.ProjectFolder, "*.achx");

        editor.Click(editor.Control<TabItem>("ProjectTab"));
        editor.Layout();
        var row = editor.Control<AnimationEditor.Views.Controls.ProjectPanelControl>("ProjectPanel").GetVisualDescendants().OfType<TreeViewItem>()
            .Single(item => item.DataContext is AnimationEditor.Views.Controls.AchxTreeNodeVm vm && vm.Name == "hero.achx");
        string focus = editor.Window.FocusManager?.GetFocusedElement()?.GetType().Name ?? "none";
        editor.RightClickAt(editor.PointIn(row, 30, 8));
        editor.Layout();
        ContextMenu menu = row.GetSelfAndVisualAncestors().OfType<Control>().Select(c => c.ContextMenu).First(m => m is not null)!;
        editor.PickMenuItem(menu, "Duplicate");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));

        string copy = Directory.GetFiles(editor.ProjectFolder, "*.achx").Except(before).Single();
        var copied = AnimationEditorHarness.ReadSaved(copy).AnimationChains[0].Frames[0];
        (field == "PropRed" ? $"{copied.Red}" : FormattableString.Invariant($"{copied.FrameLength}")).ShouldBe(text, focus);
    }
}
