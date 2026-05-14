namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>The save state reported by <see cref="IUndoManager.SaveState"/>.</summary>
    public enum SaveState
    {
        /// <summary>No file path is set; changes have never been saved.</summary>
        Unsaved,

        /// <summary>A file path is set and the last auto-save succeeded.</summary>
        AutoSaveOn,

        /// <summary>Auto-save attempted but the last write failed (e.g. read-only, disk full).</summary>
        Failed,
    }
}
