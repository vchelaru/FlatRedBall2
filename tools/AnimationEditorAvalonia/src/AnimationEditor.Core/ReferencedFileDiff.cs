using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.HotReload
{
    public static class ReferencedFileDiff
    {
        public static (IReadOnlyList<string> Added, IReadOnlyList<string> Removed) Diff(
            IEnumerable<string> oldPaths, IEnumerable<string> newPaths)
        {
            var oldSet = new HashSet<string>(oldPaths, StringComparer.OrdinalIgnoreCase);
            var newSet = new HashSet<string>(newPaths, StringComparer.OrdinalIgnoreCase);

            var added   = newSet.Where(p => !oldSet.Contains(p)).ToList();
            var removed = oldSet.Where(p => !newSet.Contains(p)).ToList();

            return (added, removed);
        }
    }
}
