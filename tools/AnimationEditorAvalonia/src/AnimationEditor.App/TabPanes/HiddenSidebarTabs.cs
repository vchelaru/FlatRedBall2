using Avalonia.Controls;

namespace AnimationEditor.App;

/// <summary>
/// Hides the sidebar tabs a tab kind has no use for while its pane shows, and brings them back after. When the
/// strip was on a tab that just got hidden, its selection moves to the fallback tab and returns on
/// <see cref="Restore"/>; otherwise the selection is left alone (#877).
/// </summary>
internal sealed class HiddenSidebarTabs
{
    private readonly TabControl _strip;
    private readonly TabItem _fallback;
    private readonly TabItem[] _tabs;
    private TabItem? _selectedBefore;

    public HiddenSidebarTabs(TabControl strip, TabItem fallback, params TabItem[] tabs)
    {
        _strip = strip;
        _fallback = fallback;
        _tabs = tabs;
    }

    public void Hide()
    {
        foreach (var tab in _tabs)
            tab.IsVisible = false;
        if (_strip.SelectedItem is TabItem { IsVisible: false } hidden)
        {
            _selectedBefore = hidden;
            _strip.SelectedItem = _fallback;
        }
    }

    public void Restore()
    {
        foreach (var tab in _tabs)
            tab.IsVisible = true;
        if (_selectedBefore is { } restore)
        {
            _strip.SelectedItem = restore;
            _selectedBefore = null;
        }
    }
}
