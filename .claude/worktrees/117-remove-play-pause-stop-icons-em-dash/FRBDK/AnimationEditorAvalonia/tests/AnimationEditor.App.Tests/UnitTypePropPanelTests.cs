using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless integration tests that verify the property panel reacts correctly when
/// the user changes the Units dropdown (Pixel / TextureCoordinate / SpriteSheet).
///
/// Each test:
///   1. Creates a headless MainWindow and selects a frame.
///   2. Changes <see cref="UnitType"/> by updating <see cref="AppState.Self.UnitType"/>
///      directly, then raises <see cref="ApplicationEvents.RaiseAnimationChainsChanged"/>
///      and flushes the dispatcher so the async InvokeAsync completes.
///   3. Asserts the visibility of <c>PropPixelSection</c>, <c>PropTcSection</c>,
///      and <c>PropTileSection</c> matches the expected layout for that unit type.
///
/// Why this matters: The section visibility is controlled entirely in
/// <c>RefreshPropertyPanel</c> in MainWindow.axaml.cs and was previously untested at
/// the UI layer. A regression here would silently show/hide the wrong input fields.
/// </summary>
public class UnitTypePropPanelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MainWindow CreateWindowWithFrame(out AnimationFrameSave frame)
    {
        var acls  = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        frame = new AnimationFrameSave
        {
            TextureName      = "dummy.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.25f,
            RightCoordinate  = 0.50f,
            TopCoordinate    = 0.0f,
            BottomCoordinate = 0.25f,
        };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        // Reset all singletons before creating the window.
        ProjectManager.Self.AnimationChainListSave = acls;
        ProjectManager.Self.FileName = null;
        SelectedState.Self.SelectedChain = null;
        SelectedState.Self.SelectedFrame = null;
        SelectedState.Self.SelectedNodes = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread = a => a();
        AppCommands.Self.ConfirmAsync = (_, _) => Task.FromResult(true);
        AppCommands.Self.FileDialogService = NullFileDialogService.Instance;
        AppState.Self.UnitType = UnitType.Pixel;

        // Create and show FIRST so the window subscribes to SelectionChanged.
        var window = new MainWindow();
        window.Show();

        // THEN set selection — this fires SelectionChanged which the window now handles.
        SelectedState.Self.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();   // flush InvokeAsync(RefreshPropertyPanel)

        return window;
    }

    /// <summary>
    /// Changes the unit type and re-triggers property panel refresh via the normal
    /// SelectionChanged pathway (same sequence as the toolbar combo firing OnUnitTypeComboChanged).
    /// </summary>
    private static void SetUnitAndRefresh(UnitType unitType)
    {
        AppState.Self.UnitType = unitType;
        // Re-assign the selected frame — fires SelectionChanged →
        // HandleSelectionChanged → InvokeAsync(RefreshPropertyPanel)
        SelectedState.Self.SelectedFrame = SelectedState.Self.SelectedFrame;
        Dispatcher.UIThread.RunJobs();
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // ── Initial state ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void PropFramePanel_WhenFrameSelected_IsVisible()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Frame was selected in CreateWindowWithFrame + RunJobs — panel must be visible.
            var panel = FindCtrl<StackPanel>(window, "PropFramePanel");
            Assert.True(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropNoneLabel_WhenNoFrameSelected_IsVisible()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Clear selection — fires SelectionChanged which refreshes the panel
            SelectedState.Self.SelectedFrame = null;
            Dispatcher.UIThread.RunJobs();

            var label = FindCtrl<TextBlock>(window, "PropNoneLabel");
            Assert.True(label.IsVisible);

            var framePanel = FindCtrl<StackPanel>(window, "PropFramePanel");
            Assert.False(framePanel.IsVisible);
        }
        finally { window.Close(); }
    }

    // ── Pixel mode ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Pixel_ShowsPixelSection_HidesTcAndTileSections()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.Pixel);

            Assert.True (FindCtrl<StackPanel>(window, "PropPixelSection").IsVisible, "PropPixelSection should be visible for Pixel");
            Assert.False(FindCtrl<StackPanel>(window, "PropTcSection"   ).IsVisible, "PropTcSection should be hidden for Pixel");
            Assert.False(FindCtrl<StackPanel>(window, "PropTileSection" ).IsVisible, "PropTileSection should be hidden for Pixel");
        }
        finally { window.Close(); }
    }

    // ── TextureCoordinate mode ────────────────────────────────────────────────

    [AvaloniaFact]
    public void TextureCoordinate_ShowsTcSection_HidesPixelAndTileSections()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.TextureCoordinate);

            Assert.False(FindCtrl<StackPanel>(window, "PropPixelSection").IsVisible, "PropPixelSection should be hidden for TC");
            Assert.True (FindCtrl<StackPanel>(window, "PropTcSection"   ).IsVisible, "PropTcSection should be visible for TC");
            Assert.False(FindCtrl<StackPanel>(window, "PropTileSection" ).IsVisible, "PropTileSection should be hidden for TC");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TextureCoordinate_PopulatesUvValuesInTcFields()
    {
        var window = CreateWindowWithFrame(out var frame);
        try
        {
            SetUnitAndRefresh(UnitType.TextureCoordinate);

            var left   = FindCtrl<NumericUpDown>(window, "PropTcLeft");
            var right  = FindCtrl<NumericUpDown>(window, "PropTcRight");
            var top    = FindCtrl<NumericUpDown>(window, "PropTcTop");
            var bottom = FindCtrl<NumericUpDown>(window, "PropTcBottom");

            Assert.Equal((decimal)frame.LeftCoordinate,   left.Value);
            Assert.Equal((decimal)frame.RightCoordinate,  right.Value);
            Assert.Equal((decimal)frame.TopCoordinate,    top.Value);
            Assert.Equal((decimal)frame.BottomCoordinate, bottom.Value);
        }
        finally { window.Close(); }
    }

    // ── SpriteSheet mode ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SpriteSheet_ShowsPixelAndTileSections_HidesTcSection()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.SpriteSheet);

            Assert.True (FindCtrl<StackPanel>(window, "PropPixelSection").IsVisible, "PropPixelSection should be visible for SpriteSheet");
            Assert.False(FindCtrl<StackPanel>(window, "PropTcSection"   ).IsVisible, "PropTcSection should be hidden for SpriteSheet");
            Assert.True (FindCtrl<StackPanel>(window, "PropTileSection" ).IsVisible, "PropTileSection should be visible for SpriteSheet");
        }
        finally { window.Close(); }
    }

    // ── Switching between modes ────────────────────────────────────────────────

    [AvaloniaFact]
    public void SwitchFromPixelToTc_UpdatesSectionsCorrectly()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.Pixel);
            Assert.True(FindCtrl<StackPanel>(window, "PropPixelSection").IsVisible);
            Assert.False(FindCtrl<StackPanel>(window, "PropTcSection").IsVisible);

            SetUnitAndRefresh(UnitType.TextureCoordinate);
            Assert.False(FindCtrl<StackPanel>(window, "PropPixelSection").IsVisible);
            Assert.True(FindCtrl<StackPanel>(window, "PropTcSection").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SwitchFromTcToSpriteSheet_ShowsTileSection()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.TextureCoordinate);
            Assert.False(FindCtrl<StackPanel>(window, "PropTileSection").IsVisible);

            SetUnitAndRefresh(UnitType.SpriteSheet);
            Assert.True(FindCtrl<StackPanel>(window, "PropTileSection").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SwitchFromSpriteSheetBackToPixel_HidesTileSection()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            SetUnitAndRefresh(UnitType.SpriteSheet);
            Assert.True(FindCtrl<StackPanel>(window, "PropTileSection").IsVisible);

            SetUnitAndRefresh(UnitType.Pixel);
            Assert.False(FindCtrl<StackPanel>(window, "PropTileSection").IsVisible);
        }
        finally { window.Close(); }
    }

    // ── Combo drives AppState ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void UnitTypeCombo_SelectedIndex_MapsToCorrectUnitType()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            var combo = FindCtrl<ComboBox>(window, "UnitTypeCombo");

            combo.SelectedIndex = (int)UnitType.Pixel;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(UnitType.Pixel, AppState.Self.UnitType);

            combo.SelectedIndex = (int)UnitType.TextureCoordinate;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(UnitType.TextureCoordinate, AppState.Self.UnitType);

            combo.SelectedIndex = (int)UnitType.SpriteSheet;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(UnitType.SpriteSheet, AppState.Self.UnitType);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void UnitTypeCombo_SpriteSheet_ShowsTileSection_EndToEnd()
    {
        // This test drives the actual UnitTypeCombo control (not AppState directly) and
        // verifies that PropTileSection becomes visible — covering the full path from the
        // toolbar combo change through OnUnitTypeComboChanged to RefreshPropertyPanel.
        var window = CreateWindowWithFrame(out _);
        try
        {
            var combo     = FindCtrl<ComboBox>(window, "UnitTypeCombo");
            var tilePanel = FindCtrl<StackPanel>(window, "PropTileSection");

            combo.SelectedIndex = (int)UnitType.SpriteSheet;
            Dispatcher.UIThread.RunJobs();

            Assert.True(tilePanel.IsVisible,
                "PropTileSection must be visible when UnitTypeCombo is set to SpriteSheet with a frame selected");
        }
        finally { window.Close(); }
    }
}
