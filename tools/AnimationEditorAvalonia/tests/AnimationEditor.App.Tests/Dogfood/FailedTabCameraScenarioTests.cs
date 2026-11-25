using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A tab whose file no longer loads is dropped; the tab that stays keeps its pan and zoom.</summary>
public class FailedTabCameraScenarioTests
{
    [AvaloniaTheory]
    [InlineData("sheet.png")]
    [InlineData("boss.png")]
    public async Task ClickingATabWhoseFileNoLongerLoads_KeepsTheCameraOfTheTabThatStays(string bossTexture)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WritePng("boss.png", 32, 32);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Attack", bossTexture, (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        editor.Wireframe.SetCamera(10, 20, 4f);
        await editor.OpenAsync(hero);
        editor.Wireframe.SetCamera(250, 150, 2f);
        var camera = editor.Wireframe.CameraState;
        File.WriteAllText(boss, "<AnimationChainArrayS");

        editor.ClickTab("boss.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.Wireframe.CameraState.ShouldBe(camera);
    }
}
