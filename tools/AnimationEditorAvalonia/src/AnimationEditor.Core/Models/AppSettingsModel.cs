using System.Collections.Generic;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Models
{
    public class AppSettingsModel
    {
        public List<string> RecentFiles { get; set; } = new List<string>();

        /// <summary>
        /// The full paths of all tabs that were open when the editor last closed.
        /// Restored on next launch so the user picks up where they left off.
        /// </summary>
        public List<string> OpenTabPaths { get; set; } = new List<string>();

        /// <summary>
        /// The full path of the tab that was active when the editor last closed.
        /// Used together with <see cref="OpenTabPaths"/> to restore the active tab on launch.
        /// </summary>
        public string? ActiveTabPath { get; set; }

        public void AddFile(FilePath filePath)
        {
            RecentFiles.RemoveAll(item => new FilePath(item) == filePath);
            RecentFiles.Insert(0, filePath.FullPath);
            while (RecentFiles.Count > 20)
            {
                RecentFiles.Remove(RecentFiles.Last());
            }
        }
    }
}
