using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Represents a single open tab in the Animation Editor.
    /// View state (zoom, pan, grid) is persisted separately in each file's companion
    /// <c>.aeproperties</c> file and restored automatically on load.
    /// </summary>
    public sealed class TabEntry
    {
        /// <param name="path">The absolute path of the <c>.achx</c> file. Pass an empty <see cref="FilePath"/> for an unsaved file.</param>
        /// <param name="displayNameOverride">
        /// When set, overrides the tab label. Use <c>"Untitled"</c> for unsaved files
        /// that have no on-disk path yet.
        /// </param>
        public TabEntry(FilePath path, string? displayNameOverride = null)
        {
            Path = path;
            _displayNameOverride = displayNameOverride;
        }

        private readonly string? _displayNameOverride;

        /// <summary>The absolute path of the <c>.achx</c> file this tab represents.</summary>
        public FilePath Path { get; }

        /// <summary>
        /// Undo/redo stack snapshot saved when this tab was last deactivated.
        /// Restored after the tab's file is reloaded on re-activation so the user's
        /// edit history persists across tab switches.
        /// </summary>
        public UndoSnapshot? UndoSnapshot { get; set; }

        /// <summary>
        /// The tab label. Returns <see cref="_displayNameOverride"/> when set;
        /// otherwise the filename without directory.
        /// </summary>
        public string DisplayName =>
            _displayNameOverride
            ?? (string.IsNullOrEmpty(Path.Original) ? "Untitled" : Path.NoPath);
    }
}
