using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System.Reflection;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerReferencedPngTests
{
    // ProjectManager is a single long-lived instance reused across File > Open calls (see
    // TabSwitchCacheTests / the LoadAnimationChain_AfterLoadTsxProject_ClearsNativeTsxState fix),
    // and ReferencedPngs is per-load state driven by the *current* achx's own ProjectFile
    // reference -- LoadAnimationChain only ever calls TryLoadProjectFile (which repopulates
    // ReferencedPngs) when the newly loaded achx declares a ProjectFile, so a second achx with no
    // ProjectFile at all must not keep reporting the first achx's referenced textures.
    [Fact]
    public void LoadAnimationChain_SecondFileHasNoProjectFile_ClearsPreviouslyLoadedReferencedPngs()
    {
        using var temp = new TempDir();
        var contentDir = Path.Combine(temp.Path, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "Hero.png"), "");

        var projectFile = Path.Combine(temp.Path, "Game.gluj");
        File.WriteAllText(projectFile,
            """
            <Project>
              <GlobalFiles>
                <ReferencedFileSave><Name>Hero.png</Name></ReferencedFileSave>
              </GlobalFiles>
            </Project>
            """);

        var pm = new ProjectManager();

        var achxWithProject = Path.Combine(temp.Path, "WithProject.achx");
        var aclsWithProject = new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            ProjectFile = "Game.gluj",
        };
        aclsWithProject.Save(achxWithProject);
        pm.LoadAnimationChain(new FilePath(achxWithProject));
        Assert.NotEmpty(pm.ReferencedPngs);

        var achxWithoutProject = Path.Combine(temp.Path, "Plain.achx");
        var aclsWithoutProject = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        aclsWithoutProject.Save(achxWithoutProject);

        pm.LoadAnimationChain(new FilePath(achxWithoutProject));

        Assert.Empty(pm.ReferencedPngs);
    }

    [Fact]
    public void TryLoadProjectFile_UsesReferencedFilesAndFiltersToPng()
    {
        using var temp = new TempDir();

        string projectFile = Path.Combine(temp.Path, "Game.gluj");
        string contentDir = Path.Combine(temp.Path, "Content");
        Directory.CreateDirectory(contentDir);

        File.WriteAllText(projectFile,
            """
            <Project>
              <Screens>
                <Screen>
                  <ReferencedFiles>
                    <ReferencedFileSave><Name>Sprites/Hero.png</Name></ReferencedFileSave>
                    <ReferencedFileSave><Name>Sprites/Hero.png</Name></ReferencedFileSave>
                    <ReferencedFileSave><Name>Data/Config.json</Name></ReferencedFileSave>
                  </ReferencedFiles>
                </Screen>
              </Screens>
              <Entities>
                <Entity>
                  <ReferencedFiles>
                    <ReferencedFileSave><Name>Enemies/Boss.png</Name></ReferencedFileSave>
                  </ReferencedFiles>
                </Entity>
              </Entities>
              <GlobalFiles>
                <ReferencedFileSave><Name>Ui/Hud.png</Name></ReferencedFileSave>
              </GlobalFiles>
            </Project>
            """);

        var sut = new ProjectManager();
        InvokeTryLoadProjectFile(sut, new FilePath(projectFile));

        var fullPaths = sut.ReferencedPngs.Select(p => p.Standardized).ToArray();

        Assert.Equal(3, fullPaths.Length);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "Sprites", "Hero.png")).Standardized, fullPaths);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "Enemies", "Boss.png")).Standardized, fullPaths);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "Ui", "Hud.png")).Standardized, fullPaths);
    }

    [Fact]
    public void TryLoadProjectFile_WhenXmlIsInvalid_FallsBackToAllPngInContent()
    {
        using var temp = new TempDir();

        string projectFile = Path.Combine(temp.Path, "Game.gluj");
        string contentDir = Path.Combine(temp.Path, "Content");
        Directory.CreateDirectory(Path.Combine(contentDir, "Sprites"));

        File.WriteAllText(projectFile, "<Project><Screens>");

        File.WriteAllText(Path.Combine(contentDir, "A.png"), "");
        File.WriteAllText(Path.Combine(contentDir, "B.PNG"), "");
        File.WriteAllText(Path.Combine(contentDir, "Note.txt"), "");
        File.WriteAllText(Path.Combine(contentDir, "Sprites", "C.png"), "");

        var sut = new ProjectManager();
        InvokeTryLoadProjectFile(sut, new FilePath(projectFile));

        var fullPaths = sut.ReferencedPngs.Select(p => p.Standardized).ToArray();

        Assert.Equal(3, fullPaths.Length);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "A.png")).Standardized, fullPaths);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "B.PNG")).Standardized, fullPaths);
        Assert.Contains(new FilePath(Path.Combine(contentDir, "Sprites", "C.png")).Standardized, fullPaths);
    }

    [Fact]
    public void TryLoadProjectFile_WhenProjectDoesNotExist_ClearsReferencedPngs()
    {
        var sut = new ProjectManager();

        InvokeTryLoadProjectFile(sut, new FilePath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gluj")));

        Assert.Empty(sut.ReferencedPngs);
    }

    private static void InvokeTryLoadProjectFile(ProjectManager projectManager, FilePath projectFile)
    {
        var method = typeof(ProjectManager).GetMethod("TryLoadProjectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(projectManager, new object[] { projectFile });
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
