using System;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnimationEditor.App.Settings;

/// <summary>Snapshot of editor settings shown in <see cref="SettingsWindowBuilder"/>.</summary>
public sealed class SettingsWindowModel
{
    public bool FileAssociationSupported { get; init; }

    public AchxFileAssociationStatus FileAssociationStatus { get; init; }

    public bool SuppressDefaultHandlerPrompt { get; init; }
}

/// <summary>Callbacks from the settings dialog back to <see cref="MainWindow"/>.</summary>
public sealed class SettingsWindowCallbacks
{
    public Action? OnSetDefaultAchx { get; init; }

    public Action<bool>? OnSuppressDefaultHandlerPromptChanged { get; init; }
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
        root.Children.Add(new ScrollViewer
        {
            Content = BuildSections(model, callbacks),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        });

        var window = new Window
        {
            Title = "Settings",
            Width = 480,
            MinWidth = 400,
            MinHeight = 200,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            Content = root,
        };
        closeBtn.Click += (_, _) => window.Close();
        return window;
    }

    /// <summary>Section stack for the settings dialog. Extracted so layout can be unit-tested without a <see cref="Window"/>.</summary>
    internal static StackPanel BuildSections(SettingsWindowModel model, SettingsWindowCallbacks callbacks)
    {
        var sections = new StackPanel { Spacing = 20 };

        if (model.FileAssociationSupported)
            sections.Children.Add(BuildFileAssociationSection(model, callbacks));

        if (sections.Children.Count == 0)
        {
            sections.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "No settings are available on this platform yet.",
            });
        }

        return sections;
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
            Children =
            {
                new TextBlock
                {
                    Text = "File association",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                },
                statusText,
                setDefaultBtn,
                suppressCheck,
            },
        };
    }
}
