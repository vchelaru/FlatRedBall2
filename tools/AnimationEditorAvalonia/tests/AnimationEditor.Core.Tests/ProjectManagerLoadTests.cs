using AnimationEditor.Core;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerLoadTests
{
    [Fact]
    public void LoadAnimationChain_MissingFile_ThrowsFileNotFoundException()
    {
        var pm = new ProjectManager();

        Assert.Throws<FileNotFoundException>(
            () => pm.LoadAnimationChain(new FilePath(@"C:\does\not\exist.achx")));
    }

    [Fact]
    public void LoadAnimationChain_MissingFile_DoesNotChangeAnimationChainListSave()
    {
        var pm = new ProjectManager();
        pm.AnimationChainListSave = null;

        try { pm.LoadAnimationChain(new FilePath(@"C:\does\not\exist.achx")); }
        catch (FileNotFoundException) { }

        Assert.Null(pm.AnimationChainListSave);
    }
}
