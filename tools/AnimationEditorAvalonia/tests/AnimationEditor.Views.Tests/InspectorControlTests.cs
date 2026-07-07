using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

// Phase 1 (#603): property display driven by ISelectedState.SelectionChanged.
// Phase 2 (#610): Rectangle/Circle panels became editable -- see
// docs/BROWSER_TREE_INSPECTOR_DECISION.md.
public class InspectorControlTests
{
    private sealed class FakeProjectManager : IProjectManager
    {
        public AnimationChainListSave? AnimationChainListSave { get; set; }
        public TileMapInformationList TileMapInformationList { get; set; } = new();
        public FilePath[] ReferencedPngs => Array.Empty<FilePath>();
        public string? FileName { get; set; }
        public TextureCoordinateType OnDiskCoordinateType { get; set; }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null) { }

        public void SaveAnimationChainList(string targetPath) { }
        public void SaveAnimationChainList(System.IO.Stream stream) { }
        public string? ResolveFilesPanelRoot() => null;
        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) => null;

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            Array.Empty<string>();
    }

    private static (InspectorControl Control, SelectedState State) Build()
    {
        var pm = new FakeProjectManager();
        var state = new SelectedState(pm);
        var control = new InspectorControl();
        control.InitializeServices(state);
        return (control, state);
    }

    [AvaloniaFact]
    public void NoSelection_ShowsPlaceholder_HidesAllPanels()
    {
        var (control, _) = Build();

        Assert.True(control.NoSelectionPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
    }

    [AvaloniaFact]
    public void FrameSelected_ShowsFramePanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave
        {
            TextureName = "hero.png",
            FrameLength = 0.125f,
            LeftCoordinate = 0.1f,
            RightCoordinate = 0.9f,
            TopCoordinate = 0.2f,
            BottomCoordinate = 0.8f,
        };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        var pm = new FakeProjectManager
        {
            AnimationChainListSave = new AnimationChainListSave(),
        };
        pm.AnimationChainListSave!.AnimationChains.Add(chain);

        state.SelectedFrame = frame;

        Assert.True(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
        Assert.False(control.NoSelectionPanel.IsVisible);
        Assert.Contains("hero.png", control.FrameTextureText.Text);
        Assert.Contains("0.125", control.FrameLengthText.Text);
    }

    [AvaloniaFact]
    public void RectSelected_ShowsRectPanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Hitbox", X = 4f, Y = -2f, ScaleX = 8f, ScaleY = 16f };
        frame.ShapesSave!.Shapes.Add(rect);

        state.SelectedFrame = frame;
        state.SelectedRectangle = rect;

        Assert.True(control.RectPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.CirclePanel.IsVisible);
        Assert.Equal("Hitbox", control.RectNameBox.Text);
        Assert.Equal(4m, control.RectXInput.Value);
        Assert.Equal(-2m, control.RectYInput.Value);
        Assert.Equal(8m, control.RectScaleXInput.Value);
        Assert.Equal(16m, control.RectScaleYInput.Value);
    }

    [AvaloniaFact]
    public void CircleSelected_ShowsCirclePanelWithValues_HidesOthers()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var circle = new CircleSave { Name = "Hurtbox", X = 1f, Y = 2f, Radius = 12f };
        frame.ShapesSave!.Shapes.Add(circle);

        state.SelectedFrame = frame;
        state.SelectedCircle = circle;

        Assert.True(control.CirclePanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
        Assert.False(control.RectPanel.IsVisible);
        Assert.Equal("Hurtbox", control.CircleNameBox.Text);
        Assert.Equal(1m, control.CircleXInput.Value);
        Assert.Equal(2m, control.CircleYInput.Value);
        Assert.Equal(12m, control.CircleRadiusInput.Value);
    }

    [AvaloniaFact]
    public void SelectionChangedDirectlyThroughSelectedState_UpdatesInspector_NotJustViaTreeClicks()
    {
        var (control, state) = Build();
        var frame = new AnimationFrameSave { TextureName = "direct.png" };

        // No tree involved at all -- InspectorControl must react to SelectedState.SelectionChanged
        // on its own, since Phase 1's tree and inspector are independent controls.
        state.SelectedFrame = frame;

        Assert.True(control.FramePanel.IsVisible);
        Assert.Contains("direct.png", control.FrameTextureText.Text);

        state.Reset();

        Assert.True(control.NoSelectionPanel.IsVisible);
        Assert.False(control.FramePanel.IsVisible);
    }

    // Phase 2 follow-up (#610): editable Rectangle/Circle panels, routed through
    // AppCommands.SetRectProps/SetCircleProps. CommitRectProps/CommitCircleProps are the directly
    // -testable seams (mirror MainWindow's own ApplyRectProps/ApplyCircleProps), exercised here
    // without simulating an actual LostFocus/KeyDown.

    private static (InspectorControl Control, IAppCommands Commands, AARectSave Rect, CircleSave Circle, SelectedState State)
        BuildWithEditableShapes()
    {
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Hitbox", X = 4f, Y = -2f, ScaleX = 8f, ScaleY = 16f };
        var circle = new CircleSave { Name = "Hurtbox", X = 1f, Y = 2f, Radius = 12f };
        frame.ShapesSave!.Shapes.Add(rect);
        frame.ShapesSave!.Shapes.Add(circle);
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);

        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var state = new SelectedState(pm);
        var events = new ApplicationEvents();
        var appState = new AppState(events, state);
        var ioManager = new IoManager(appState);
        var objectFinder = new ObjectFinder(pm);
        var undoManager = new UndoManager();
        var appCommands = new AppCommands(pm, state, events, ioManager, objectFinder, undoManager);

        var control = new InspectorControl();
        control.InitializeServices(state);
        control.EnableEditing(appCommands);

        state.SelectedFrame = frame;
        state.SelectedRectangle = rect;

        return (control, appCommands, rect, circle, state);
    }

    [AvaloniaFact]
    public void CommitRectProps_AppliesEditedFieldValues()
    {
        var (control, _, rect, _, _) = BuildWithEditableShapes();

        control.RectNameBox.Text = "Hitbox2";
        control.RectXInput.Value = 10m;
        control.RectYInput.Value = 20m;
        control.RectScaleXInput.Value = 5m;
        control.RectScaleYInput.Value = 6m;
        control.CommitRectProps();

        Assert.Equal("Hitbox2", rect.Name);
        Assert.Equal(10f, rect.X);
        Assert.Equal(20f, rect.Y);
        Assert.Equal(5f, rect.ScaleX);
        Assert.Equal(6f, rect.ScaleY);
    }

    [AvaloniaFact]
    public void CommitCircleProps_AppliesEditedFieldValues()
    {
        var (control, _, _, circle, state) = BuildWithEditableShapes();
        // Switch selection to the circle (BuildWithEditableShapes leaves the rect selected).
        state.SelectedCircle = circle;

        control.CircleNameBox.Text = "Hurtbox2";
        control.CircleXInput.Value = 30m;
        control.CircleYInput.Value = 40m;
        control.CircleRadiusInput.Value = 9m;
        control.CommitCircleProps();

        Assert.Equal("Hurtbox2", circle.Name);
        Assert.Equal(30f, circle.X);
        Assert.Equal(40f, circle.Y);
        Assert.Equal(9f, circle.Radius);
    }

    [AvaloniaFact]
    public void CommitRectProps_NoSelection_DoesNotThrow()
    {
        var control = new InspectorControl();
        var pm = new FakeProjectManager();
        var state = new SelectedState(pm);
        control.InitializeServices(state);

        control.CommitRectProps(); // no EnableEditing call, no selection -- should just no-op
    }

    // Live testing found a real bug: editing exactly one NumericUpDown field (e.g. via its spin
    // buttons, which never trigger LostFocus) produced no visible change until a *second* field
    // was also edited. EnableEditing wired the Rect/Circle numeric fields to LostFocus, but
    // CommitRectProps/CommitCircleProps were always directly testable (bypassing the wiring), so
    // no existing test exercised the actual event that fires when a field is edited. These tests
    // drive the real wiring (setting .Value, matching desktop's own ValueChanged-based
    // PropRectX/PropRectY/etc.) instead of calling CommitRectProps directly.

    [AvaloniaFact]
    public void EditingSingleRectField_ViaValueChanged_CommitsImmediately()
    {
        var (control, _, rect, _, _) = BuildWithEditableShapes();

        control.RectXInput.Value = 99m;

        Assert.Equal(99f, rect.X);
    }

    [AvaloniaFact]
    public void EditingSingleCircleField_ViaValueChanged_CommitsImmediately()
    {
        var (control, _, _, circle, state) = BuildWithEditableShapes();
        state.SelectedCircle = circle;

        control.CircleRadiusInput.Value = 42m;

        Assert.Equal(42f, circle.Radius);
    }

    [AvaloniaFact]
    public void SwitchingSelectionBetweenRects_DoesNotLeakStaleFieldValues()
    {
        var (control, _, rectA, _, state) = BuildWithEditableShapes();
        var frame = state.SelectedFrame!;
        var rectB = new AARectSave { Name = "Other", X = 100f, Y = 200f, ScaleX = 30f, ScaleY = 40f };
        frame.ShapesSave!.Shapes.Add(rectB);

        // Edit rectA's fields (each ValueChanged commits immediately per the fix above), then
        // switch selection to rectB. Populating the panel for rectB sets each field in turn
        // (Name, X, Y, ScaleX, ScaleY) -- if ValueChanged commits fire during that populate, an
        // early field set (rectB's new X) paired with a not-yet-updated later field (still
        // rectA's old Y/ScaleX/ScaleY at that instant) would corrupt rectB with rectA's leftovers.
        control.RectXInput.Value = 1m;
        control.RectYInput.Value = 2m;
        control.RectScaleXInput.Value = 3m;
        control.RectScaleYInput.Value = 4m;

        state.SelectedRectangle = rectB;

        Assert.Equal(100f, rectB.X);
        Assert.Equal(200f, rectB.Y);
        Assert.Equal(30f, rectB.ScaleX);
        Assert.Equal(40f, rectB.ScaleY);
    }
}
