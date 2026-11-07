using AnimationEditor.App.Controls;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Navigation;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

/// <summary>
/// Back and forward between places the user jumped between on purpose (Go to Definition, a click on the tab strip),
/// like a browser. Plain selection clicks never record anything; the selection is read when the user leaves.
/// </summary>
public partial class MainWindow
{
    private readonly NavigationHistory _navigationHistory = new();
    private bool _navigatingHistory;

    private void WireNavigation()
    {
        // Tunnel, so the buttons work wherever the pointer is and no control reacts to them first.
        AddHandler(PointerPressedEvent, (object? _, PointerPressedEventArgs e) =>
        {
            if (MouseNavigationDirection(e.GetCurrentPoint(this).Properties.PointerUpdateKind) is not { } direction) return;
            e.Handled = true;
            _ = NavigateHistoryAsync(back: direction < 0);
        }, RoutingStrategies.Tunnel);
        MenuNavBack.Click += (_, _) => _ = NavigateHistoryAsync(back: true);
        MenuNavForward.Click += (_, _) => _ = NavigateHistoryAsync(back: false);
        UpdateNavigationMenuItems();
    }

    /// <summary>-1 for the mouse Back button, +1 for Forward, null for any other press.</summary>
    internal static int? MouseNavigationDirection(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.XButton1Pressed => -1,
        PointerUpdateKind.XButton2Pressed => 1,
        _ => null,
    };

    private void UpdateNavigationMenuItems()
    {
        MenuNavBack.IsEnabled = _navigationHistory.CanGoBack;
        MenuNavForward.IsEnabled = _navigationHistory.CanGoForward;
    }

    /// <summary>The active tab and what is selected in it, or null when no tab is open.</summary>
    private NavigationLocation? CaptureNavigationLocation()
    {
        if (_tabManager.ActiveTab is not { } tab) return null;
        string path = tab.Path.Original ?? string.Empty;
        switch (tab.Kind)
        {
            case TabKind.Sequence:
                var timeline = SequenceTimeline;
                return new NavigationLocation(
                    path,
                    SequenceIndex: SequenceList.SelectedIndex >= 0 ? SequenceList.SelectedIndex : null,
                    TimelineTrack: timeline.Selection?.Track ?? timeline.SelectedTrack,
                    TimelineChain: timeline.Selection?.Chain);
            case TabKind.Achx:
                var chain = _selectedState.SelectedChain;
                int? frameIndex = _selectedState.SelectedFrame is { } frame && chain is not null
                    ? chain.Frames.IndexOf(frame)
                    : null;
                return new NavigationLocation(path, ChainName: chain?.Name, FrameIndex: frameIndex);
            default:
                return new NavigationLocation(path);
        }
    }

    /// <summary>Remembers <paramref name="before"/> when the user has since moved somewhere else.</summary>
    private void PushHistoryIfMoved(NavigationLocation? before)
    {
        if (before is not { } leaving || _navigatingHistory) return;
        if (CaptureNavigationLocation() != leaving)
            _navigationHistory.Push(leaving);
        UpdateNavigationMenuItems();
    }

    /// <summary>A tab-strip click: activates <paramref name="tab"/> and makes the tab it came from a history entry.</summary>
    private async Task ActivateTabFromStripAsync(TabEntry tab)
    {
        var before = CaptureNavigationLocation();
        await ActivateTabAsync(tab);
        PushHistoryIfMoved(before);
    }

    private async Task NavigateHistoryAsync(bool back)
    {
        if (_navigatingHistory) return;
        var here = CaptureNavigationLocation() ?? new NavigationLocation(string.Empty);
        _navigatingHistory = true;
        try
        {
            var target = back
                ? _navigationHistory.Back(here, CanReach)
                : _navigationHistory.Forward(here, CanReach);
            if (target is { } location)
                await RestoreNavigationLocationAsync(location);
        }
        finally
        {
            _navigatingHistory = false;
            UpdateNavigationMenuItems();
        }
    }

    /// <summary>A location is reachable while its tab is open, or its file still exists to reopen.</summary>
    private bool CanReach(NavigationLocation location)
    {
        if (string.IsNullOrEmpty(location.Path)) return false;
        var path = new FilePath(location.Path);
        if (_tabManager.Tabs.Any(t => t.Path == path)) return true;
        return !TabManager.IsUntitledSentinel(location.Path) && File.Exists(path.FullPath);
    }

    private async Task RestoreNavigationLocationAsync(NavigationLocation location)
    {
        var path = new FilePath(location.Path);
        if (_tabManager.Tabs.FirstOrDefault(t => t.Path == path) is { } open)
            await ActivateTabAsync(open);
        else
            await OpenFileAsTab(path.FullPath);
        // The tree rebuild is dispatched by the load; let it run before looking anything up in it.
        await Dispatcher.UIThread.InvokeAsync(() => { });

        if (_tabManager.ActiveTab is not { } active || active.Path != path) return;
        if (active.Kind == TabKind.Sequence)
            RestoreSequenceSelection(location);
        else if (active.Kind == TabKind.Achx)
            RestoreAchxSelection(location);
    }

    private void RestoreAchxSelection(NavigationLocation location)
    {
        var chain = _projectManager.AnimationChainListSave?.AnimationChains
            .FirstOrDefault(c => c.Name == location.ChainName);
        if (chain is null) return;
        // Chain first: setting it clears the frame.
        _selectedState.SelectedChain = chain;
        if (location.FrameIndex is >= 0 and var index && index < chain.Frames.Count)
            _selectedState.SelectedFrame = chain.Frames[index];
        SyncTreeSelection();
    }

    private void RestoreSequenceSelection(NavigationLocation location)
    {
        if (location.SequenceIndex is >= 0 and var sequence && sequence < SequenceList.ItemCount
            && SequenceList.SelectedIndex != sequence)
            SequenceList.SelectedIndex = sequence;
        if (location.TimelineTrack is not { } track) return;
        SequenceTimeline.Select(
            location.TimelineChain is { } chain ? new TimelineChainId(track, chain) : null,
            track);
    }
}
