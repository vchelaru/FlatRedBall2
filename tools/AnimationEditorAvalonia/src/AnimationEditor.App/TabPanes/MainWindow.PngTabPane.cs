using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace AnimationEditor.App;

public partial class MainWindow
{
    /// <summary>
    /// A <c>.png</c> tab (issue #604): a read-only image preview with its git history in the
    /// sidebar. It has no document, undo history or companion file, so it captures nothing on
    /// leave and only app-wide hotkeys and menu items work while it is active.
    /// </summary>
    private sealed class PngTabPane : ITabPane
    {
        private readonly MainWindow _window;

        // Latest-wins guard + in-flight handle for the async decode, mirroring the history load.
        private int _textureLoadId;

        // Inspector and History, hidden during a PNG preview (#686). Built on first Show, since panes
        // are constructed before the window's controls exist.
        private HiddenSidebarTabs? _hiddenTabs;

        public PngTabPane(MainWindow window) => _window = window;

        public Task TextureLoadInFlight { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Shows the Diff/blame pane in the <c>TopPane</c> cell the ANIMATIONS tree fills for a
        /// document (#877), so the sidebar splitter behaves the same in either mode. A
        /// PNG has no editable properties or undo history, so Inspector and History hide too;
        /// Files/Textures stays (#875 follow-up) since its Project scope browses PNGs on its own.
        /// The bottom strip's selection only moves when it was on a tab that just got hidden.
        /// </summary>
        public void Show()
        {
            var w = _window;
            w.PngPaneGrid.IsVisible = true;
            w.PngPane.IsVisible = true;
            w.DiffBlamePane.IsVisible = true;
            _hiddenTabs ??= new HiddenSidebarTabs(w.SidebarTabs, w.FilesTab, w.InspectorTab, w.HistoryTab);
            _hiddenTabs.Hide();
        }

        public void Hide()
        {
            var w = _window;
            w.PngPane.LoadTexture(null);   // release the previewed image
            w.PngPane.IsVisible = false;
            w.PngPaneGrid.IsVisible = false;
            w.DiffBlamePane.IsVisible = false;
            _hiddenTabs?.Restore();
        }

        public void CaptureLeaving(TabEntry tab) { }

        /// <summary>
        /// Decodes the image off the UI thread so the tab appears immediately even for a large
        /// sheet; a "Loading…" overlay shows until it's ready. The history load is likewise async.
        /// </summary>
        public Task<bool> LoadAsync(TabEntry tab, TabLoad load)
        {
            _window.ResetPngUsageOverlay();
            TextureLoadInFlight = LoadTextureAsync(tab.Path.FullPath);
            _window.LoadBlameForPng(tab.Path.FullPath);
            return Task.FromResult(true);
        }

        private async Task LoadTextureAsync(string absolutePath)
        {
            int id = ++_textureLoadId;
            _window.PngLoadingText.IsVisible = true;
            try
            {
                await _window.PngPane.LoadTextureAsync(absolutePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PngTab] texture load failed: {ex}");
            }
            finally
            {
                // Only the newest load clears the indicator, so a superseded load can't hide it early.
                if (id == _textureLoadId)
                    _window.PngLoadingText.IsVisible = false;
            }
        }

        public Task<TabEntry?> PrepareCloseAsync(TabEntry tab) => Task.FromResult<TabEntry?>(tab);

        // Never reached: Save is document-scoped, which this pane refuses.
        public Task SaveAsync(TabEntry? tab) => Task.CompletedTask;

        public void Closing(TabEntry tab) { }

        public TabStatus Status(TabEntry? tab) => new(string.Empty, null, tab?.DisplayName ?? string.Empty, string.Empty);

        public string? TitlePath(TabEntry? tab) => tab?.Path.FullPath;

        public bool Accepts(HotkeyScope scope) => scope == HotkeyScope.App;

        // Detaching re-opens the file in a fresh editor window, which only knows how to load achx.
        public bool CanDetach => false;

        public bool HasProjectPanelRow(TabEntry tab) => false;
    }
}
