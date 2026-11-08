using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.Models;
using Avalonia.Media;
using System.Threading.Tasks;

namespace AnimationEditor.App;

/// <summary>How <see cref="ITabPane.LoadAsync"/> was reached, for kinds that load differently at startup.</summary>
internal enum TabLoad
{
    /// <summary>The user switched to the tab, or it became active because another tab closed.</summary>
    Switch,

    /// <summary>The tab was the active one in the previous session and is being reopened at startup.</summary>
    Restore,
}

/// <summary>The status bar's contents for the active tab. A null <see cref="SaveBrush"/> hides the save dot.</summary>
internal readonly record struct TabStatus(string SaveLabel, IBrush? SaveBrush, string FileName, string Counts);

/// <summary>
/// One tab kind's share of <see cref="MainWindow"/>: the pane it shows, how a tab of that kind is
/// loaded, left and closed, and what the status bar, title, hotkeys and menu items do while it is active.
/// <see cref="MainWindow"/> keeps one per <see cref="TabKind"/> and routes tab lifecycle through
/// <c>PaneFor</c> instead of branching on the kind. Panes stay alive for the window's lifetime and
/// are shown and hidden, never rebuilt.
/// </summary>
internal interface ITabPane
{
    /// <summary>Shows this kind's main pane and sidebar. Called only when the visible kind changes.</summary>
    void Show();

    /// <summary>Hides what <see cref="Show"/> showed, before another kind's pane is shown.</summary>
    void Hide();

    /// <summary>Snapshots a tab of this kind that is being switched away from. Not called when it closes.</summary>
    void CaptureLeaving(TabEntry tab);

    /// <summary>
    /// Loads <paramref name="tab"/> into the pane, which is already showing. Returns false when the
    /// content could not be loaded and whatever was live before is still live.
    /// </summary>
    Task<bool> LoadAsync(TabEntry tab, TabLoad load);

    /// <summary>
    /// Runs before <paramref name="tab"/> closes (a save prompt, say). Returns the tab to close,
    /// which a save may have replaced, or null to keep it open.
    /// </summary>
    Task<TabEntry?> PrepareCloseAsync(TabEntry tab);

    /// <summary>
    /// File &gt; Save for <paramref name="tab"/>, the active tab (null when none is open). Only called while
    /// <see cref="Accepts"/> takes <see cref="HotkeyScope.Document"/>.
    /// </summary>
    Task SaveAsync(TabEntry? tab);

    /// <summary>Cleans up after the user discards <paramref name="tab"/>, just before it leaves the strip.</summary>
    void Closing(TabEntry tab);

    /// <summary>The status bar while this kind is active. <paramref name="tab"/> is null when no tab is open.</summary>
    TabStatus Status(TabEntry? tab);

    /// <summary>The file the title bar names while this kind is active, or null for none.</summary>
    string? TitlePath(TabEntry? tab);

    /// <summary>
    /// Whether hotkeys and menu items of <paramref name="scope"/> act while this kind is active.
    /// Menu items of a refused scope are disabled.
    /// </summary>
    bool Accepts(HotkeyScope scope);

    /// <summary>
    /// The Delete hotkey: deletes what is selected in this kind's document. Only called while <see cref="Accepts"/>
    /// takes <see cref="HotkeyScope.Document"/>.
    /// </summary>
    void DeleteSelection();

    /// <summary>
    /// The Ctrl+C hotkey and Edit &gt; Copy: copies what is selected in this kind's document. Only called while
    /// <see cref="Accepts"/> takes <see cref="HotkeyScope.Document"/>; does nothing when this kind has nothing to copy now.
    /// </summary>
    void CopySelection();

    /// <summary>The Ctrl+V hotkey and Edit &gt; Paste, the counterpart of <see cref="CopySelection"/>.</summary>
    void PasteSelection();

    /// <summary>
    /// The Ctrl+X hotkey and Edit &gt; Cut: <see cref="CopySelection"/>, then removes what was copied. Only called while
    /// <see cref="Accepts"/> takes <see cref="HotkeyScope.Document"/>.
    /// </summary>
    void CutSelection();

    /// <summary>
    /// The Ctrl+D hotkey and Edit &gt; Duplicate: copies what is selected in place. Only called while <see cref="Accepts"/>
    /// takes <see cref="HotkeyScope.Document"/>.
    /// </summary>
    void DuplicateSelection();

    /// <summary>
    /// File &gt; Reload from Disk: reads this kind's document from its file again, discarding what the file lacks. Only
    /// called while <see cref="Accepts"/> takes <see cref="HotkeyScope.Document"/>.
    /// </summary>
    void ReloadFromDisk();

    /// <summary>Whether Edit &gt; Copy, Cut, Paste and Duplicate are enabled while this kind is active.</summary>
    bool CanCopyPaste { get; }

    /// <summary>
    /// The Space hotkey: plays or pauses this kind's preview. Only called while <see cref="Accepts"/> takes
    /// <see cref="HotkeyScope.Document"/>.
    /// </summary>
    void TogglePlayback();

    /// <summary>
    /// Whether a single click on a file in the Animations browser opens it in the preview tab. False for a kind that
    /// takes files dragged from the browser: pressing a row to drag it also selects it, and opening a preview there
    /// would switch away from the tab being dropped on. Double-click still opens the file.
    /// </summary>
    bool BrowserClickOpensPreview { get; }

    /// <summary>Whether the tab's context menu offers Detach to New Window.</summary>
    bool CanDetach { get; }

    /// <summary>Whether <paramref name="tab"/> has a row in the Project panel to keep selected while it is active.</summary>
    bool HasProjectPanelRow(TabEntry tab);
}
