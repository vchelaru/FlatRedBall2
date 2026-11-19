using AnimationEditor.Core.Data;
using AnimationEditor.Core.Paths;
using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A tab's saved camera is applied in the same turn as its document, so a tab switch never paints one frame
/// with the previous tab's zoom and pan before the saved ones land.
/// </summary>
public class TabSwitchCameraScenarioTests
{
    [AvaloniaFact]
    public async Task LoadingACompanionFile_AppliesItsCameraBeforeTheDispatcherRuns()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "hero.achx");
        await editor.OpenAsync(path);

        editor.Services.IoManager.SaveCompanionFileFor(new FilePath(path), new AESettingsSave
        {
            WireframeZoomPercent = 200,
            WireframePanX = 33f,
            WireframePanY = 44f,
            PreviewZoomPercent = 300,
        });
        editor.Wireframe.SetCamera(0, 0, 1);

        // No dispatcher pump between the load and the read: a camera that is posted would still be the old one.
        editor.Services.IoManager.LoadAndApplyCompanionFileFor(path);

        var (panX, panY, zoom) = editor.Wireframe.CameraState;
        panX.ShouldBe(33f);
        panY.ShouldBe(44f);
        zoom.ShouldBe(2f, tolerance: 0.01f);
        editor.Preview.Zoom.ShouldBe(3f, tolerance: 0.01f);
    }
}
