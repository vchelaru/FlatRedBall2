using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using AnimationEditor.Core.Models;

namespace AnimationEditor.App.Settings;

/// <summary>Snapshot of editor settings shown in <see cref="SettingsWindowBuilder"/>.</summary>
public sealed class SettingsWindowModel
{
    /// <summary>The current editor theme.</summary>
    public AppTheme Theme { get; init; }

    /// <summary>Current canvas-background override (packed <c>0xAARRGGBB</c>), or <c>null</c> for the theme default.</summary>
    public uint? CanvasBackgroundArgb { get; init; }

    /// <summary>The active theme's default canvas background (packed <c>0xAARRGGBB</c>), shown when no override is set.</summary>
    public uint ThemeDefaultBackgroundArgb { get; init; }

    /// <summary>Current guide-line color override (packed <c>0xAARRGGBB</c>), or <c>null</c> for the theme default.</summary>
    public uint? GuideLineArgb { get; init; }

    /// <summary>The active theme's default guide-line color (packed <c>0xAARRGGBB</c>), shown when no override is set.</summary>
    public uint ThemeDefaultGuideLineArgb { get; init; }

    /// <summary>Whether wireframe frame rectangles are drawn with a fill in addition to the stroke outline (#976).</summary>
    public bool FillFrameRectangles { get; init; }
}

/// <summary>Callbacks from the settings dialog back to <see cref="MainWindow"/>.</summary>
public sealed class SettingsWindowCallbacks
{
    /// <summary>
    /// Invoked when a theme radio is chosen. Applies the theme and returns the new theme's default
    /// background and guide-line colors (packed <c>0xAARRGGBB</c>) so swatches showing "Theme Default" can refresh.
    /// </summary>
    public Func<AppTheme, (uint Background, uint GuideLine)>? OnThemeChanged { get; init; }

    /// <summary>Invoked with the new packed <c>0xAARRGGBB</c> value (<c>null</c> = theme default) when the canvas background changes.</summary>
    public Action<uint?>? OnCanvasBackgroundChanged { get; init; }

    /// <summary>Opens a custom-color picker seeded with the current background; returns the chosen packed ARGB, or <c>null</c> if cancelled.</summary>
    public Func<Task<uint?>>? OnPickCustomCanvasBackground { get; init; }

    /// <summary>Invoked with the new packed <c>0xAARRGGBB</c> value (<c>null</c> = theme default) when the guide-line color changes.</summary>
    public Action<uint?>? OnGuideLineChanged { get; init; }

    /// <summary>Opens a custom-color picker seeded with the current guide-line color; returns the chosen packed ARGB, or <c>null</c> if cancelled.</summary>
    public Func<Task<uint?>>? OnPickCustomGuideLine { get; init; }

    /// <summary>Invoked with the new value when the "Fill frame rectangles" checkbox changes.</summary>
    public Action<bool>? OnFillFrameRectanglesChanged { get; init; }
}

/// <summary>Builds the editor settings window. New sections belong here as the dialog grows.</summary>
public static class SettingsWindowBuilder
{
    public static Window Build(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        var closeBtn = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 80,
        };

        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(closeBtn, Dock.Bottom);
        root.Children.Add(closeBtn);
        root.Children.Add(BuildTabs(model, callbacks));

        var window = new Window
        {
            Title = "Settings",
            Width = 480,
            MinWidth = 400,
            MinHeight = 200,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Content = root,
        };
        closeBtn.Click += (_, _) => window.Close();
        return window;
    }

    /// <summary>
    /// Tab strip for the settings dialog. Extracted so layout can be unit-tested without a
    /// <see cref="Window"/>. Each category is its own tab rather
    /// than a flat scrolling list of sections, so the dialog can keep growing (grid, rulers, etc.)
    /// without becoming an ever-taller scroll.
    /// </summary>
    internal static TabControl BuildTabs(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Appearance", Content = InTab(BuildAppearanceSection(model, callbacks)) },
            },
        };

        return tabs;
    }

    private static ScrollViewer InTab(Control content) => new()
    {
        Content = content,
        Padding = new Thickness(0, 12, 0, 0),
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    // Named background presets (packed 0xAARRGGBB, opaque). Guide-line color has no named
    // presets — arbitrary named colors don't help there the way Black/White/Mid Gray do for a
    // canvas fill — so it only offers Theme Default and Custom.
    private static readonly (string Name, uint Argb)[] _backgroundPresets =
    {
        ("Black", 0xFF000000),
        ("White", 0xFFFFFFFF),
        ("Mid Gray", 0xFF808080),
    };

    private static Control BuildAppearanceSection(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        var backgroundRow = BuildColorRow(
            "Background",
            model.CanvasBackgroundArgb,
            model.ThemeDefaultBackgroundArgb,
            _backgroundPresets,
            callbacks.OnCanvasBackgroundChanged,
            callbacks.OnPickCustomCanvasBackground);
        var guideLineRow = BuildColorRow(
            "Guide line",
            model.GuideLineArgb,
            model.ThemeDefaultGuideLineArgb,
            Array.Empty<(string, uint)>(),
            callbacks.OnGuideLineChanged,
            callbacks.OnPickCustomGuideLine);

        var fillCheck = new CheckBox
        {
            Content = "Fill Selection",
            IsChecked = model.FillFrameRectangles,
        };
        fillCheck.IsCheckedChanged += (_, _) =>
        {
            if (fillCheck.IsChecked is bool value)
                callbacks.OnFillFrameRectanglesChanged?.Invoke(value);
        };

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BuildThemeRow(model.Theme, callbacks.OnThemeChanged, backgroundRow, guideLineRow),
                backgroundRow.Control,
                guideLineRow.Control,
                fillCheck,
            },
        };
    }

    /// <summary>
    /// One labeled color row: a swatch reflecting the current color and a dropdown of "Theme Default",
    /// the named presets, and "Custom…" (which defers to <paramref name="onPickCustom"/>; a cancelled
    /// picker restores the previous choice). The swatch is also a button that opens the picker, since
    /// re-selecting an already-selected "Custom…" raises no event.
    /// </summary>
    private static ColorRow BuildColorRow(
        string label,
        uint? currentArgb,
        uint initialThemeDefaultArgb,
        (string Name, uint Argb)[] presets,
        Action<uint?>? onChanged,
        Func<Task<uint?>>? onPickCustom)
    {
        var themeDefaultArgb = initialThemeDefaultArgb;
        var overrideArgb = currentArgb;
        var swatch = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(3),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromUInt32(currentArgb ?? themeDefaultArgb)),
        };

        const string themeDefaultItem = "Theme Default";
        const string customItem = "Custom…";
        var items = new List<string> { themeDefaultItem };
        items.AddRange(presets.Select(p => p.Name));
        items.Add(customItem);

        var combo = new ComboBox { ItemsSource = items, MinWidth = 140 };
        var suppressSelection = false;

        void Select(string item)
        {
            suppressSelection = true;
            combo.SelectedItem = item;
            suppressSelection = false;
        }

        string ItemFor(uint? argb) =>
            argb is null ? themeDefaultItem
            : presets.Any(p => p.Argb == argb) ? presets.First(p => p.Argb == argb).Name
            : customItem;

        void SetColor(uint? argb)
        {
            overrideArgb = argb;
            swatch.Background = new SolidColorBrush(Color.FromUInt32(argb ?? themeDefaultArgb));
            onChanged?.Invoke(argb);
        }

        // Returns false when the picker was cancelled (or there is none), so callers can restore the old choice.
        async Task<bool> PickCustomAsync()
        {
            if (onPickCustom is null || await onPickCustom() is not uint picked) return false;
            SetColor(picked);
            return true;
        }

        Select(ItemFor(currentArgb));
        combo.SelectionChanged += async (_, _) =>
        {
            if (suppressSelection || combo.SelectedItem is not string choice) return;
            if (choice == themeDefaultItem)
                SetColor(null);
            else if (choice == customItem)
            {
                if (!await PickCustomAsync()) Select(ItemFor(overrideArgb));
            }
            else
                SetColor(presets.First(p => p.Name == choice).Argb);
        };

        // Re-selecting "Custom…" while it is already chosen raises no event, so the swatch also opens the picker.
        var swatchButton = new Button { Content = swatch, Padding = new Thickness(4) };
        ToolTip.SetTip(swatchButton, "Choose a custom color");
        swatchButton.Click += async (_, _) =>
        {
            if (await PickCustomAsync()) Select(customItem);
        };

        var control = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { swatchButton, combo } },
            },
        };

        return new ColorRow(control, newThemeDefault =>
        {
            themeDefaultArgb = newThemeDefault;
            if (overrideArgb is null)
                swatch.Background = new SolidColorBrush(Color.FromUInt32(themeDefaultArgb));
        });
    }

    /// <summary>A color row plus a hook that re-points its "Theme Default" color after the theme changes.</summary>
    private sealed record ColorRow(Control Control, Action<uint> SetThemeDefault);

    /// <summary>
    /// Light / Dark / Follow System radios. Changing the theme re-points the color rows' "Theme Default"
    /// swatches, since those depend on the active theme.
    /// </summary>
    private static Control BuildThemeRow(
        AppTheme current,
        Func<AppTheme, (uint Background, uint GuideLine)>? onChanged,
        ColorRow backgroundRow,
        ColorRow guideLineRow)
    {
        var radios = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var (name, theme) in new[] { ("Light", AppTheme.Light), ("Dark", AppTheme.Dark), ("Follow System", AppTheme.System) })
        {
            var radio = new RadioButton { Content = name, GroupName = "Theme", IsChecked = theme == current };
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked != true || onChanged is null) return;
                var (background, guideLine) = onChanged(theme);
                backgroundRow.SetThemeDefault(background);
                guideLineRow.SetThemeDefault(guideLine);
            };
            radios.Children.Add(radio);
        }

        return new StackPanel
        {
            Spacing = 4,
            Children = { new TextBlock { Text = "Theme" }, radios },
        };
    }
}
