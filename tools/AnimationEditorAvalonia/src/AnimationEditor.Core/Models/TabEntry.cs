using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation.Content;
using System;

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
            Kind = ResolveKind(path);
        }

        private readonly string? _displayNameOverride;

        /// <summary>The absolute path of the file this tab represents.</summary>
        public FilePath Path { get; }

        /// <summary>
        /// Which view this tab drives, derived from the file extension at construction.
        /// See <see cref="TabKind"/>.
        /// </summary>
        public TabKind Kind { get; }

        /// <summary>
        /// Determines the <see cref="TabKind"/> for <paramref name="path"/> by extension.
        /// A <c>.png</c> (any case) is <see cref="TabKind.Png"/>; everything else — including
        /// untitled sentinels and empty paths — is <see cref="TabKind.Achx"/>.
        /// </summary>
        public static TabKind ResolveKind(FilePath path) =>
            path.Extension == "png" ? TabKind.Png : TabKind.Achx;

        /// <summary>
        /// Undo/redo stack snapshot saved when this tab was last deactivated.
        /// Restored after the tab's file is reloaded on re-activation so the user's
        /// edit history persists across tab switches.
        /// </summary>
        public UndoSnapshot? UndoSnapshot { get; set; }

        /// <summary>
        /// In-memory editor model for this tab. Populated when the tab is deactivated and
        /// reused on re-activation when the on-disk file has not changed.
        /// </summary>
        public AnimationChainListSave? CachedEditorModel { get; set; }

        /// <summary>
        /// <see cref="IProjectManager.OnDiskCoordinateType"/> captured with
        /// <see cref="CachedEditorModel"/>.
        /// </summary>
        public TextureCoordinateType CachedOnDiskCoordinateType { get; set; } = TextureCoordinateType.Pixel;

        /// <summary>
        /// UTC last-write time of <see cref="Path"/> when <see cref="CachedEditorModel"/> was
        /// captured. Used to detect external edits while the tab was in the background.
        /// </summary>
        public DateTime? CachedDiskWriteTimeUtc { get; set; }

        /// <summary>
        /// Name of the selected animation chain when this tab was last deactivated.
        /// Session-only (not written to <c>.aeproperties</c>).
        /// </summary>
        public string? CachedSelectedChainName { get; set; }

        /// <summary>
        /// Index of the selected frame within <see cref="CachedSelectedChainName"/> when this
        /// tab was last deactivated, or <c>null</c> when only the chain was selected.
        /// Session-only (not written to <c>.aeproperties</c>).
        /// </summary>
        public int? CachedSelectedFrameIndex { get; set; }

        /// <summary>
        /// The tab label. Returns <see cref="_displayNameOverride"/> when set;
        /// otherwise the filename without directory.
        /// </summary>
        public string DisplayName =>
            _displayNameOverride
            ?? (string.IsNullOrEmpty(Path.Original) ? "Untitled" : Path.NoPath);
    }
}
