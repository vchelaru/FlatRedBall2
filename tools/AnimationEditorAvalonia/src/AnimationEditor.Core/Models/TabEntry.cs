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
        public TabEntry(FilePath path)
        {
            Path = path;
        }

        /// <summary>The absolute path of the <c>.achx</c> file this tab represents.</summary>
        public FilePath Path { get; }

        /// <summary>The filename without directory, used as the tab label.</summary>
        public string DisplayName => Path.NoPath;
    }
}
