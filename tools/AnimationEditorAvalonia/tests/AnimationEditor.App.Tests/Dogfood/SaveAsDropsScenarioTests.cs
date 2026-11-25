using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Save As to .achj saves, then says what it left out: what the open document carries and only an .achx holds (#2192).</summary>
public class SaveAsDropsScenarioTests
{
    [AvaloniaTheory]
    [InlineData("<ColorKey>0</ColorKey>", "boss.achj", "Saved boss.achj, but not everything is kept — <ColorKey> in 2 animations ('Idle' and 'Walk') is not kept.")]
    [InlineData("<ColorKey>0</ColorKey>", "boss.achx", null)]
    [InlineData("<ColorKey></ColorKey>", "boss.achj", null)]
    public async Task SaveAs_SaysWhatTheFileDoesNotKeep(string inChain, string picked, string? problem)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 16, 16);
        string hero = Path.Combine(editor.ProjectFolder, "hero.achx"), boss = Path.Combine(editor.ProjectFolder, picked);
        // <Custom> is not read, so the open document does not hold it.
        File.WriteAllText(hero, $"<AnimationChainArraySave><CoordinateType>Pixel</CoordinateType><AnimationChain><Name>Idle</Name>{inChain}</AnimationChain><AnimationChain><Name>Walk</Name>{inChain}"
            + "<Frame><TextureName>sheet.png</TextureName><FrameLength>1</FrameLength><RightCoordinate>16</RightCoordinate><BottomCoordinate>16</BottomCoordinate><Custom>5</Custom></Frame></AnimationChain></AnimationChainArraySave>");
        await editor.OpenAsync(hero);

        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldBe(problem);
        editor.TabLabels.ShouldBe([picked]);
        File.Exists(boss).ShouldBeTrue();
    }
}
