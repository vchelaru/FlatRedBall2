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

        /// <summary>
        /// The editor theme. Defaults to <see cref="AppTheme.Dark"/> so existing settings
        /// files (which predate this field) and fresh installs keep the historical dark look.
        /// </summary>
        public AppTheme Theme { get; set; } = AppTheme.Dark;

        /// <summary>
        /// Optional override for the canvas background behind both editor panels, as a packed
        /// <c>0xAARRGGBB</c> value. <c>null</c> follows the theme default. This is a per-user
        /// viewing preference only — it is never written into the <c>.achx</c>.
        /// </summary>
        public uint? CanvasBackgroundArgb { get; set; }

        /// <summary>
        /// Optional override for the guide-line color drawn in the preview panel, as a packed
        /// <c>0xAARRGGBB</c> value. <c>null</c> follows the theme default. Like
        /// <see cref="CanvasBackgroundArgb"/>, this is a per-user viewing preference only.
        /// </summary>
        public uint? GuideLineArgb { get; set; }

        /// <summary>
        /// When <c>true</c>, the editor never offers to register itself as the default
        /// application for <c>.achx</c> files. Set when the user clicks "Don't show again"
        /// on the file-association prompt. Defaults to <c>false</c> so the prompt can appear
        /// once on a fresh install.
        /// </summary>
        public bool SuppressDefaultHandlerPrompt { get; set; }

        /// <summary>
        /// UTC time of the last update check (startup or forced). <c>null</c> means never
        /// checked. See <see cref="Update.UpdateCheckPolicy"/> for the cache window this backs.
        /// </summary>
        public System.DateTime? LastUpdateCheckUtc { get; set; }

        /// <summary>
        /// The latest released version as of <see cref="LastUpdateCheckUtc"/>, or <c>null</c>
        /// if no update was known at that time. Lets a cache-hit path (within the check window)
        /// report the same result without re-hitting the GitHub API.
        /// </summary>
        public string? LatestKnownUpdateVersion { get; set; }

        /// <summary>Release page URL paired with <see cref="LatestKnownUpdateVersion"/>.</summary>
        public string? LatestKnownUpdateUrl { get; set; }

        /// <summary>
        /// The folder last picked via File → Open Project Folder (#770). Rescanned on the next
        /// launch to repopulate the Project tab without requiring a re-pick. Left stale (not
        /// cleared) if the folder no longer exists -- the startup check just skips it, same as
        /// <see cref="OpenTabPaths"/> silently dropping paths that no longer exist on disk.
        /// </summary>
        public string? LastProjectFolderPath { get; set; }

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
