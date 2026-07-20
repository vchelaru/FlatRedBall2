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

    public event EventHandler? Toggled;

    public FolderExpanderIcon() => InitializeComponent();

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Toggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
