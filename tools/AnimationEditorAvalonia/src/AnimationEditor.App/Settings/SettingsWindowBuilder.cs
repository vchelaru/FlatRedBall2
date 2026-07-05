using System;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnimationEditor.App.Settings;

/// <summary>Snapshot of editor settings shown in <see cref="SettingsWindowBuilder"/>.</summary>
public sealed class SettingsWindowModel
{
    public bool FileAssociationSupported { get; init; }

    public AchxFileAssociationStatus FileAssociationStatus { get; init; }

    public bool SuppressDefaultHandlerPrompt { get; init; }

    /// <summary>Current canvas-background override (packed <c>0xAARRGGBB</c>), or <c>null</c> for the theme default.</summary>
    public uint? CanvasBackgroundArgb { get; init; }

    /// <summary>The active theme's default canvas background (packed <c>0xAARRGGBB</c>), shown when no override is set.</summary>
    public uint ThemeDefaultBackgroundArgb { get; init; }

    /// <summary>Current guide-line color override (packed <c>0xAARRGGBB</c>), or <c>null</c> for the theme default.</summary>
    public uint? GuideLineArgb { get; init; }

    /// <summary>The active theme's default guide-line color (packed <c>0xAARRGGBB</c>), shown when no override is set.</summary>
    public uint ThemeDefaultGuideLineArgb { get; init; }
}

/// <summary>Callbacks from the settings dialog back to <see cref="MainWindow"/>.</summary>
public sealed class SettingsWindowCallbacks
{
    public Action? OnSetDefaultAchx { get; init; }

    public Action<bool>? OnSuppressDefaultHandlerPromptChanged { get; init; }

    /// <summary>Invoked with the new packed <c>0xAARRGGBB</c> value (<c>null</c> = theme default) when the canvas background changes.</summary>
    public Action<uint?>? OnCanvasBackgroundChanged { get; init; }

    /// <summary>Opens a custom-color picker seeded with the current background; returns the chosen packed ARGB, or <c>null</c> if cancelled.</summary>
    public Func<Task<uint?>>? OnPickCustomCanvasBackground { get; init; }

    /// <summary>Invoked with the new packed <c>0xAARRGGBB</c> value (<c>null</c> = theme default) when the guide-line color changes.</summary>
    public Action<uint?>? OnGuideLineChanged { get; init; }

    /// <summary>Opens a custom-color picker seeded with the current guide-line color; returns the chosen packed ARGB, or <c>null</c> if cancelled.</summary>
    public Func<Task<uint?>>? OnPickCustomGuideLine { get; init; }
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
    /// <see cref="Window"/>. Each category (colors, file association, ...) is its own tab rather
    /// than a flat scrolling list of sections, so the dialog can keep growing (grid, rulers, etc.)
    /// without becoming an ever-taller scroll.
    /// </summary>
    internal static TabControl BuildTabs(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Colors", Content = InTab(BuildCanvasColorsSection(model, callbacks)) },
            },
        };

        if (model.FileAssociationSupported)
        {
            tabs.Items.Add(new TabItem
            {
                Header = "File Association",
                Content = InTab(BuildFileAssociationSection(model, callbacks)),
            });
        }

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

    private static Control BuildCanvasColorsSection(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BuildColorRow(
                    "Background",
                    model.CanvasBackgroundArgb,
                    model.ThemeDefaultBackgroundArgb,
                    _backgroundPresets,
                    callbacks.OnCanvasBackgroundChanged,
                    callbacks.OnPickCustomCanvasBackground),
                BuildColorRow(
                    "Guide line",
                    model.GuideLineArgb,
                    model.ThemeDefaultGuideLineArgb,
                    Array.Empty<(string, uint)>(),
                    callbacks.OnGuideLineChanged,
                    callbacks.OnPickCustomGuideLine),
            },
        };
    }

    /// <summary>
    /// One labeled color row: a swatch reflecting the current color, a "Theme Default" button,
    /// a button per named preset, and a "Custom…" button that defers to <paramref name="onPickCustom"/>.
    /// Buttons (not checkable toggles) so re-picking "Custom…" always re-opens the picker even
    /// when it's already the active color — there is no "checked" state to keep in sync.
    /// </summary>
    private static Control BuildColorRow(
        string label,
        uint? currentArgb,
        uint themeDefaultArgb,
        (string Name, uint Argb)[] presets,
        Action<uint?>? onChanged,
        Func<Task<uint?>>? onPickCustom)
    {
        var swatch = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(3),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromUInt32(currentArgb ?? themeDefaultArgb)),
        };

        void SetColor(uint? argb)
        {
            swatch.Background = new SolidColorBrush(Color.FromUInt32(argb ?? themeDefaultArgb));
            onChanged?.Invoke(argb);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { swatch } };

        var themeBtn = new Button { Content = "Theme Default" };
        themeBtn.Click += (_, _) => SetColor(null);
        buttons.Children.Add(themeBtn);

        foreach (var (name, argb) in presets)
        {
            var presetBtn = new Button { Content = name };
            presetBtn.Click += (_, _) => SetColor(argb);
            buttons.Children.Add(presetBtn);
        }

        var customBtn = new Button { Content = "Custom…" };
        customBtn.Click += async (_, _) =>
        {
            if (onPickCustom is null) return;
            if (await onPickCustom() is uint picked) SetColor(picked);
        };
        buttons.Children.Add(customBtn);

        return new StackPanel
        {
            Spacing = 4,
            Children = { new TextBlock { Text = label }, buttons },
        };
    }

    private static Control BuildFileAssociationSection(
        SettingsWindowModel model,
        SettingsWindowCallbacks callbacks)
    {
        var statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = AchxFileAssociationStatusFormatter.Describe(model.FileAssociationStatus),
        };

        var setDefaultBtn = new Button
        {
            Content = "Set as default for .achx files…",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        setDefaultBtn.Click += (_, _) => callbacks.OnSetDefaultAchx?.Invoke();

        var suppressCheck = new CheckBox
        {
            Content = "Don't show startup prompt for .achx association",
            IsChecked = model.SuppressDefaultHandlerPrompt,
        };
        suppressCheck.IsCheckedChanged += (_, _) =>
        {
            if (suppressCheck.IsChecked is bool value)
                callbacks.OnSuppressDefaultHandlerPromptChanged?.Invoke(value);
        };

        return new StackPanel
        {
            Spacing = 10,
            Children = { statusText, setDefaultBtn, suppressCheck },
        };
    }
}
