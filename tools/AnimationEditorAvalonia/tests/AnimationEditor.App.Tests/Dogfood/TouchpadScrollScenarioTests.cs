using Vector = Avalonia.Vector;
using Vector2 = System.Numerics.Vector2;
using AnimationEditor.Core.Input;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A touchpad's two-finger scroll pans the wireframe (top) and the preview (bottom) alike, while a
/// mouse wheel and Ctrl+scroll keep zooming (#1237, #1238). The OS-specific detector is faked here;
/// the classification rules are covered by <c>WheelGestureTests</c> in Core.Tests.
/// </summary>
public class TouchpadScrollScenarioTests
{
    private const int Sheet = 128;

    private sealed class FixedDetector(WheelSource source) : IWheelSourceDetector
    {
        public WheelReading Read(Vector2 delta, bool controlHeld) => new(source);
    }

    private static async Task<AnimationEditorHarness> OpenWalkAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        return editor;
    }

    [AvaloniaFact]
    public async Task TouchpadScrollOverTheWireframe_PansTheCanvasAndKeepsTheZoom()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync();
        editor.Wireframe.WheelSourceDetector = new FixedDetector(WheelSource.MacTrackpad);
        (float panX, float panY, float zoom) = editor.Wireframe.CameraState;

        editor.Scroll(editor.WireframePointAt(64, 64), new Vector(0.2, -0.1));

        (float newPanX, float newPanY, float newZoom) = editor.Wireframe.CameraState;
        (newPanX - panX).ShouldBe(10f, 0.001f);
        (newPanY - panY).ShouldBe(-5f, 0.001f);
        newZoom.ShouldBe(zoom);
        editor.Wireframe.IsZoomAnimating.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task TouchpadScrollOverThePreview_PansTheCanvasAndKeepsTheZoom()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync();
        editor.Preview.WheelSourceDetector = new FixedDetector(WheelSource.MacTrackpad);
        (float panX, float panY) = editor.Preview.PanOffset;
        float zoom = editor.Preview.Zoom;

        editor.Scroll(editor.PreviewPointAt(0, 0), new Vector(0.2, -0.1));

        (float newPanX, float newPanY) = editor.Preview.PanOffset;
        (newPanX - panX).ShouldBe(10f, 0.001f);
        (newPanY - panY).ShouldBe(-5f, 0.001f);
        editor.Preview.Zoom.ShouldBe(zoom);
        editor.Preview.IsZoomAnimating.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task CtrlScrollOnATouchpad_ZoomsBothCanvasesInsteadOfPanning()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync();
        editor.Wireframe.WheelSourceDetector = new FixedDetector(WheelSource.WindowsTouchpad);
        editor.Preview.WheelSourceDetector = new FixedDetector(WheelSource.WindowsTouchpad);
        (float wirePanX, _, float wireZoom) = editor.Wireframe.CameraState;
        (float previewPanX, _) = editor.Preview.PanOffset;

        editor.Scroll(editor.WireframePointAt(64, 64), new Vector(0, 1), RawInputModifiers.Control);
        editor.Scroll(editor.PreviewPointAt(0, 0), new Vector(0, 1), RawInputModifiers.Control);
        await editor.WaitUntilAsync(() => !editor.Wireframe.IsZoomAnimating && !editor.Preview.IsZoomAnimating, TimeSpan.FromSeconds(2));

        editor.Wireframe.CameraState.Zoom.ShouldBeGreaterThan(wireZoom);
        editor.Wireframe.CameraState.PanX.ShouldNotBe(wirePanX);   // zooming toward the pivot moved the content, not a pan scroll
        editor.Preview.Zoom.ShouldBeGreaterThan(1f);
        previewPanX.ShouldBe(editor.Preview.PanOffset.X, 0.5f);
    }

    [AvaloniaFact]
    public async Task MouseWheelSource_StillZoomsAtTheCursor()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync();
        editor.Wireframe.WheelSourceDetector = new FixedDetector(WheelSource.Wheel);
        float zoom = editor.Wireframe.CameraState.Zoom;

        editor.Scroll(editor.WireframePointAt(64, 64), new Vector(0, 1));
        await editor.WaitUntilAsync(() => !editor.Wireframe.IsZoomAnimating, TimeSpan.FromSeconds(2));

        editor.Wireframe.CameraState.Zoom.ShouldBeGreaterThan(zoom);
    }

    [AvaloniaFact]
    public async Task ScrollPan_RaisesPanChangedOnceTheFingersStop_NotOnEveryEvent()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync();
        editor.Wireframe.WheelSourceDetector = new FixedDetector(WheelSource.LinuxTouchpad);
        int raised = 0;
        editor.Wireframe.PanChanged += (_, _) => raised++;

        for (int i = 0; i < 5; i++)
        {
            editor.Scroll(editor.WireframePointAt(64, 64), new Vector(0.1, 0.1));
        }
        raised.ShouldBe(0);

        (await editor.WaitUntilAsync(() => raised > 0, TimeSpan.FromSeconds(2))).ShouldBeTrue("the pan settles and is saved");
        raised.ShouldBe(1);
    }
}
