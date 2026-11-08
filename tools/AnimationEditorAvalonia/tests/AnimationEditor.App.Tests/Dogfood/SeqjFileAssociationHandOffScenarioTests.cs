using Avalonia.Headless.XUnit;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Double-clicking a <c>.seqj</c> in Explorer runs <c>AnimationEditor.exe "path"</c>. A second process hands the path to the
/// running editor through <c>MainWindow.OpenFileAsTab</c> (App.axaml.cs); a first process reads it from the command line into the
/// same <c>LoadAnimationFileAsync</c>. Both must open a sequence tab.
/// </summary>
public class SeqjFileAssociationHandOffScenarioTests
{
    [AvaloniaFact]
    public async Task SeqjHandedToARunningEditor_OpensASequenceTabNextToTheOpenAchx()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(hero);

        await editor.OpenAsync(seqj);

        editor.TabLabels.ShouldContain("hero.achx");
        editor.TabLabels.ShouldContain("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
    }
}
