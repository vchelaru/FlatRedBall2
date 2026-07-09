using Avalonia.Controls;

namespace AnimationEditor.App.Helpers;

/// <summary>
/// Wires a hidden-by-default sidebar <see cref="TabItem"/> to a checkable <see cref="MenuItem"/>:
/// clicking the menu item shows and selects the tab, clicking it again hides the tab (falling back
/// to another tab if it was selected) and keeps the menu item's checkmark in sync throughout.
/// </summary>
internal sealed class ToggleableSidebarTab
{
    private readonly TabControl _tabs;
    private readonly TabItem _tab;
    private readonly TabItem _fallbackTab;
    private readonly MenuItem _menuItem;

    public ToggleableSidebarTab(TabControl tabs, TabItem tab, TabItem fallbackTab, MenuItem menuItem)
    {
        _tabs = tabs;
        _tab = tab;
        _fallbackTab = fallbackTab;
        _menuItem = menuItem;

        _tab.IsVisible = false;
        _menuItem.IsChecked = false;
        _menuItem.Click += (_, _) => Toggle();
    }

    private void Toggle()
    {
        if (_tab.IsVisible) Hide();
        else Show();
    }

    private void Show()
    {
        _tab.IsVisible = true;
        _tabs.SelectedItem = _tab;
        _menuItem.IsChecked = true;
    }

    /// <summary>Hides the tab, e.g. from a close button or middle-click on the tab header.</summary>
    public void Hide()
    {
        _tab.IsVisible = false;
        if (ReferenceEquals(_tabs.SelectedItem, _tab))
            _tabs.SelectedItem = _fallbackTab;
        _menuItem.IsChecked = false;
    }
}
