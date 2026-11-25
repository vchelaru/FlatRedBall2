using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A copy of an animation whose name ends in a number too big to count on.</summary>
public class CopyNameNumberScenarioTests
{
    [AvaloniaTheory]
    [InlineData(Key.D)]
    [InlineData(Key.V)]
    public async Task NameEndingInALongNumber_IsCopied(Key key)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Capture20260101123456", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Capture20260101123456"));

        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(key, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBeNull();
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Count.ShouldBe(2);
    }
}
