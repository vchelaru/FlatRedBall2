using System.Linq;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class OpenFileFiltersTests
{
    [Fact]
    public void AnimationFiles_FirstEntry_CoversEveryOpenableFormat()
    {
        var first = OpenFileFilters.AnimationFiles[0];

        Assert.Equal(new[] { "*.achx", "*.achj", "*.tsx" }, first.Patterns);
    }

    [Fact]
    public void AnimationFiles_PerFormatEntries_FollowTheCombinedEntry()
    {
        var perFormat = OpenFileFilters.AnimationFiles.Skip(1).SelectMany(f => f.Patterns).OrderBy(p => p);

        Assert.Equal(new[] { "*.achj", "*.achx", "*.tsx" }, perFormat);
    }
}
