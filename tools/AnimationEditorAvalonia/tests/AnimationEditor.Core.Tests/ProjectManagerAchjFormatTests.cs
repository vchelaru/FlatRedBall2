using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

// ProjectManager dispatches between the .achx (XML) and .achj (JSON) dialects purely from the
// file extension -- no separate "on disk format" flag. Loading a .achj file, then saving back to
// the same FileName, round-trips as JSON; loading a .achx file keeps saving as XML (#872 follow-up).
public class ProjectManagerAchjFormatTests
{
    [Fact]
    public void LoadAnimationChain_AchjFile_ParsesJsonContent()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/test.achj";
        System.IO.File.WriteAllText(path,
            "{\"coordinateType\":\"Pixel\",\"animationChains\":[{\"name\":\"Walk\",\"frames\":[{\"textureName\":\"walk.png\",\"frameLength\":0.1}]}]}");

        pm.LoadAnimationChain(new FilePath(path));

        Assert.Equal("Walk", pm.AnimationChainListSave!.AnimationChains.Single().Name);
    }

    [Fact]
    public void SaveAnimationChainList_AchjPath_WritesJsonThatReloadsThroughFromJsonFile()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/test.achj";
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });

        pm.SaveAnimationChainList(path);
        var reloaded = AnimationChainListSave.FromJsonFile(path);

        Assert.Equal("Idle", reloaded.AnimationChains.Single().Name);
    }

    [Fact]
    public void SaveAnimationChainList_AchxPath_StillWritesXml()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/test.achx";
        pm.AnimationChainListSave = new AnimationChainListSave();
        pm.AnimationChainListSave.AnimationChains.Add(new AnimationChainSave { Name = "Run" });

        pm.SaveAnimationChainList(path);
        var reloaded = AnimationChainListSave.FromFile(path);

        Assert.Equal("Run", reloaded.AnimationChains.Single().Name);
    }

    [Fact]
    public void LoadAnimationChain_AchjFile_ThenSaveWithSameFileName_RoundTripsAsJson()
    {
        var pm = new ProjectManager();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/loaded.achj";
        var original = new AnimationChainListSave();
        original.AnimationChains.Add(new AnimationChainSave { Name = "Attack" });
        original.SaveJson(path);

        pm.LoadAnimationChain(new FilePath(path));
        pm.SaveAnimationChainList(pm.FileName!);

        // Would throw a JSON parse exception if the extension-preserving Save had instead
        // written XML to the .achj path.
        var reloaded = AnimationChainListSave.FromJsonFile(path);
        Assert.Equal("Attack", reloaded.AnimationChains.Single().Name);
    }
}
