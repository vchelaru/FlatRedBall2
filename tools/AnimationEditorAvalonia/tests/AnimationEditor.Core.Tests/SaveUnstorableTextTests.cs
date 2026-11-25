using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// A document holding a name XML cannot store (an .achj loads with one) reports it once when saved to XML, names the
/// character, and keeps the document and the file on disk as they were (#2046).
/// </summary>
[Collection("SequentialSingletons")]
public class SaveUnstorableTextTests : IDisposable
{
    private static readonly string Bad = "Bad" + (char)1;
    private static readonly string Problem = $"\"Bad{(char)0xFFFD}\" contains the character U+0001, which XML cannot store.";
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();
    private readonly List<string> _failures = [];

    public SaveUnstorableTextTests()
    {
        _ctx.AppCommands.SaveFailed += _failures.Add;
        _ctx.AppCommands.SaveAsRefused += _failures.Add;
    }

    public void Dispose() => _dir.Dispose();

    private string At(string name) => Path.Combine(_dir.Path, name);

    [Fact]
    public async Task SaveAs_FromAchjToAchx_ReportsOnceAndKeepsTheDocument()
    {
        var file = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        file.AnimationChains.Add(new AnimationChainSave { Name = Bad });
        file.SaveJson(At("bad.achj"));
        (await _ctx.AppCommands.OpenAchxWorkflowAsync(At("bad.achj"))).ShouldBeTrue();
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(At("other.achx"));

        (await _ctx.AppCommands.SaveCurrentAnimationChainListAsync()).ShouldBeFalse();

        _failures.ShouldBe(["Could not save other.achx: " + Problem]);
        new FilePath(_ctx.ProjectManager.FileName!).ShouldBe(new FilePath(At("bad.achj")));
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name.ShouldBe(Bad);
        File.Exists(At("other.achx")).ShouldBeFalse();
        _ctx.AppCommands.SaveCurrentAnimationChainList().ShouldBeTrue();
    }

    [Fact]
    public void Save_OfATsx_ReportsOnceAndKeepsTheFile()
    {
        File.WriteAllText(At("tiles.tsx"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="Heroes.png" width="64" height="64"/>
             <tile id="1">
              <animation>
               <frame tileid="1" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        string before = File.ReadAllText(At("tiles.tsx"));
        _ctx.ProjectManager.LoadTsxProject(new FilePath(At("tiles.tsx")));
        _ctx.AppCommands.AutosaveOnChange = false;
        _ctx.AppCommands.RenameChain(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(), Bad);

        _ctx.AppCommands.SaveCurrentAnimationChainList(At("tiles.tsx")).ShouldBeFalse();

        _failures.ShouldBe([Problem]);
        File.ReadAllText(At("tiles.tsx")).ShouldBe(before);
    }
}
