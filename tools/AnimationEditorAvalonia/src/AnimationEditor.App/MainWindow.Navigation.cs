using AnimationEditor.App.Controls;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Navigation;
using AnimationEditor.Core.Sequences;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App;

/// <summary>
/// A global back and forward history, like a browser's. Any pointer press or release, key press, tab switch, file
/// open or tab close is a user action (<see cref="NavigationTracker"/>); when it moves the active tab or the
/// selection, the place it started from becomes a history entry. Undo, Back and Forward, loads and playback happen
/// outside any action (or suppress it) and record nothing. The place is read fresh when an action starts.
/// </summary>
public partial class MainWindow
{
    private readonly NavigationHistory _navigationHistory = new();
    private readonly Stopwatch _navigationClock = Stopwatch.StartNew();
    private NavigationTracker _navigation = null!;
    private bool _navigatingHistory;

    private void WireNavigation()
    {
        _navigation = new NavigationTracker(
            _navigationHistory, CaptureNavigationLocation, IsStillPresent, () => _navigationClock.Elapsed);

        // Every real input is a user action. The action stays open until the dispatcher is idle, so selection changes
        // the input triggers (including posted catch-ups) land inside it; a drag's later moves are not new actions.
        AddHandler(PointerPressedEvent, (object? _, PointerPressedEventArgs e) =>
        {
            if (MouseNavigationDirection(e.GetCurrentPoint(this).Properties.PointerUpdateKind) is null)
                BeginInputAction();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (object? _, PointerReleasedEventArgs e) => BeginInputAction(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            if (IsNavigationInputKey(e.Key, e.KeyModifiers)) BeginInputAction();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Tunnel, so the buttons work wherever the pointer is and no control reacts to them first.
        AddHandler(PointerPressedEvent, (object? _, PointerPressedEventArgs e) =>
        {
            if (MouseNavigationDirection(e.GetCurrentPoint(this).Properties.PointerUpdateKind) is not { } direction) return;
            e.Handled = true;
            _ = NavigateHistoryAsync(back: direction < 0);
        }, RoutingStrategies.Tunnel);
        MenuNavBack.Click += (_, _) => _ = NavigateHistoryAsync(back: true);
        MenuNavForward.Click += (_, _) => _ = NavigateHistoryAsync(back: false);
        _navigationHistory.Changed += UpdateNavigationMenuItems;
        UpdateNavigationMenuItems();
    }

    private void BeginInputAction()
    {
        var action = _navigation.BeginUserAction();
        Dispatcher.UIThread.Post(action.Dispose, DispatcherPriority.Background);
    }

    /// <summary>
    /// Whether a key press can be a navigation: not a lone modifier, and not a Ctrl/Cmd or Alt chord (undo, paste,
    /// save, Alt+Left/Right are commands, not selection moves).
    /// </summary>
    internal static bool IsNavigationInputKey(Key key, KeyModifiers modifiers) =>
        key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        && (modifiers & (KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Alt)) == 0;

    private void Undo()
    {
        using (_navigation.Suppress()) _undoManager.Undo();
    }

    private void Redo()
    {
        using (_navigation.Suppress()) _undoManager.Redo();
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

    private async Task NavigateHistoryAsync(bool back)
    {
        if (_navigatingHistory) return;
        var here = CaptureNavigationLocation() ?? new NavigationLocation(string.Empty);
        _navigatingHistory = true;
        using var suppress = _navigation.Suppress();
        try
        {
            var target = back
                ? _navigationHistory.Back(here, IsStillPresent)
                : _navigationHistory.Forward(here, IsStillPresent);
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

    /// <summary>
    /// <see cref="CanReach"/>, plus: in the active tab, the chain, frame or sequence must still exist. A chain the
    /// user just renamed or deleted is not a place worth coming back to.
    /// </summary>
    private bool IsStillPresent(NavigationLocation location)
    {
        if (!CanReach(location)) return false;
        if (_tabManager.ActiveTab is not { } active || active.Path != new FilePath(location.Path)) return true;
        if (active.Kind == TabKind.Achx && location.ChainName is { } name)
        {
            var chain = _projectManager.AnimationChainListSave?.AnimationChains.FirstOrDefault(c => c.Name == name);
            return chain is not null && (location.FrameIndex is not { } frame || frame < chain.Frames.Count);
        }
        if (active.Kind == TabKind.Sequence && location.SequenceIndex is { } sequence)
            return sequence < SequenceList.ItemCount;
        return true;
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
