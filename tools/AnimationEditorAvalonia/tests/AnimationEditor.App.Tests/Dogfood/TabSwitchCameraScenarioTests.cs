using AnimationEditor.Core.Data;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A tab owns its wireframe camera: it comes from the tab's own memory (seeded once from the companion file), a tab
/// switch swaps the camera together with the document, and no other camera is ever shown in between.
/// </summary>
public class TabSwitchCameraScenarioTests
{
    private static string WriteTab(AnimationEditorHarness editor, string name)
    {
        editor.WriteAchx(name, AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        return Path.Combine(editor.ProjectFolder, name);
    }

    [AvaloniaFact]
    public async Task FirstShowOfATab_TakesItsCameraFromTheCompanionFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 512, 512);
        string path = WriteTab(editor, "hero.achx");
        editor.Services.IoManager.SaveCompanionFileFor(new FilePath(path), new AESettingsSave
        {
            WireframeZoomPercent = 200,
            WireframePanX = 33f,
            WireframePanY = 44f,
        });

        await editor.OpenAsync(path);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        var (panX, panY, zoom) = editor.Wireframe.CameraState;
        panX.ShouldBe(33f);
        panY.ShouldBe(44f);
        zoom.ShouldBe(2f, tolerance: 0.01f);
    }

    [AvaloniaFact]
    public async Task TwoTabsOnTheSameTexture_EachKeepTheirOwnCamera_WithNoCameraShownInBetween()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 512, 512);
        string hero = WriteTab(editor, "hero.achx");
        string boss = WriteTab(editor, "boss.achx");

        await editor.OpenAsync(hero);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        editor.Wireframe.SetCamera(300, 200, 1f);
        await editor.OpenAsync(boss);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        editor.Wireframe.SetCamera(250, 150, 2f);
        var bossCamera = editor.Wireframe.CameraState;

        var seen = new List<(float PanX, float PanY, float Zoom)>();
        editor.Wireframe.ViewChanged += () => seen.Add(editor.Wireframe.CameraState);
        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(500));

        var heroCamera = editor.Wireframe.CameraState;
        heroCamera.PanX.ShouldBe(300f);
        heroCamera.PanY.ShouldBe(200f);
        heroCamera.Zoom.ShouldBe(1f, tolerance: 0.01f);
        seen.ShouldNotBeEmpty("the switch must have raised a view change for this to prove anything");
        // Only the camera being left and the camera being arrived at ever appear: no centred or borrowed camera.
        foreach (var camera in seen)
            (camera == bossCamera || camera == heroCamera).ShouldBeTrue($"unexpected camera {camera} (boss {bossCamera}, hero {heroCamera})");
    }
}
