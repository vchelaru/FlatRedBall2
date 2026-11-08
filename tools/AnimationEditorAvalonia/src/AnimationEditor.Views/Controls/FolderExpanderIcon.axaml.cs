using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Reusable open/closed folder glyph for a folder-tree row. Fires <see cref="Toggled"/> on click;
/// callers own what "toggled" means for their own tree-node view-model (each tree uses a different
/// node VM type, so this widget stays deliberately dumb about that). See #770 follow-up.
/// </summary>
public partial class FolderExpanderIcon : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FolderExpanderIcon, bool>(nameof(IsOpen));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private const string OpenIconPath = "avares://AnimationEditor.Views/Assets/icons/svg/IconFolderOpen.svg";
    private const string ClosedIconPath = "avares://AnimationEditor.Views/Assets/icons/svg/IconFolder.svg";

    public static readonly StyledProperty<string> IconPathProperty =
        AvaloniaProperty.Register<FolderExpanderIcon, string>(nameof(IconPath), ClosedIconPath);

    /// <summary>The SVG for the current <see cref="IsOpen"/> state.</summary>
    public string IconPath
    {
        get => GetValue(IconPathProperty);
        private set => SetValue(IconPathProperty, value);
    }

    public event EventHandler? Toggled;

    public FolderExpanderIcon() => InitializeComponent();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            IconPath = IsOpen ? OpenIconPath : ClosedIconPath;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Toggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
