using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Icon-toggled inline search box shared by any folder/tree panel that wants a filter field
/// (#770 follow-up; first consumer is <see cref="ProjectPanelControl"/>). Owns only presentation
/// and the toggle/clear/Escape/click-away interactions -- what a query change means is entirely
/// up to the caller via <see cref="QueryChanged"/>.
/// </summary>
public partial class SearchToggleBox : UserControl
{
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SearchToggleBox, string?>(nameof(Placeholder));

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// Focus moving into this control (typically the tree the box filters) does not collapse
    /// the box -- mirrors MainWindow's AnimTree click-away exception so clicking a filtered
    /// result doesn't yank the box closed out from under the click.
    /// </summary>
    public static readonly StyledProperty<Control?> KeepOpenWhenFocusMovesIntoProperty =
        AvaloniaProperty.Register<SearchToggleBox, Control?>(nameof(KeepOpenWhenFocusMovesInto));

    public Control? KeepOpenWhenFocusMovesInto
    {
        get => GetValue(KeepOpenWhenFocusMovesIntoProperty);
        set => SetValue(KeepOpenWhenFocusMovesIntoProperty, value);
    }

    /// <summary>Raised on every keystroke with the box's current text (empty when cleared).</summary>
    public event EventHandler<string>? QueryChanged;

    public SearchToggleBox()
    {
        InitializeComponent();

        SearchToggleBtn.Click += (_, _) => Toggle();

        // PropertyChanged on TextProperty rather than TextChanged: TextChanged is only raised
        // from TextBox's internal editing pipeline (real keystrokes), not a direct Text
        // assignment -- PropertyChanged fires for both, and real typing updates Text either way.
        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                QueryChanged?.Invoke(this, SearchBox.Text ?? string.Empty);
        };

        // Two-stage clear: with text, clear it (box stays open); when already empty, collapse.
        SearchClearBtn.Click += (_, _) =>
        {
            if (TreeSearchBoxLogic.ClearShouldCollapse(SearchBox.Text))
                Clear();
            else
            {
                SearchBox.Text = string.Empty;
                SearchBox.Focus();
            }
        };

        SearchBox.AddHandler(
            InputElement.KeyDownEvent,
            (object? _, KeyEventArgs e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Clear();
                    e.Handled = true;
                }
            },
            RoutingStrategies.Tunnel);

        SearchBox.LostFocus += (_, _) =>
            Dispatcher.UIThread.Post(CollapseOnClickAway, DispatcherPriority.Background);
    }

    public void Expand()
    {
        SearchToggleBtn.IsVisible = false;
        SearchBox.IsVisible = true;
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Background);
    }

    /// <summary>Collapses the box, restores the toggle icon, and clears the query (synchronously
    /// firing <see cref="QueryChanged"/> with an empty string). Public so a caller can clear the
    /// filter from outside, e.g. after the user picks a result from the filtered list.</summary>
    public void Clear()
    {
        SearchBox.IsVisible = false;
        SearchToggleBtn.IsVisible = true;
        SearchBox.Text = string.Empty;
    }

    private void Toggle()
    {
        if (SearchBox.IsVisible) Clear();
        else Expand();
    }

    private void CollapseOnClickAway()
    {
        if (!SearchBox.IsVisible) return;

        var topLevel = TopLevel.GetTopLevel(this);
        var focused = topLevel?.FocusManager?.GetFocusedElement() as Visual;
        bool focusInBox = focused is not null &&
            (ReferenceEquals(focused, SearchBox) || focused.GetVisualAncestors().Contains(SearchBox));

        var keepOpenTarget = KeepOpenWhenFocusMovesInto;
        bool focusInTarget = keepOpenTarget is not null && focused is not null &&
            (ReferenceEquals(focused, keepOpenTarget) || focused.GetVisualAncestors().Contains(keepOpenTarget));

        if (!focusInBox && !focusInTarget)
            Clear();
    }
}
