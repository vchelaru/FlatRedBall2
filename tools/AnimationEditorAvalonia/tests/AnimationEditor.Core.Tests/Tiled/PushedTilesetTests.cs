using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// #2136: an editor before it wrote an .achx's animations into a .tsx and tagged those tiles. Such a file opens like any
/// other, and an old .tiledsync beside an .achx means nothing.
/// </summary>
[Collection("SequentialSingletons")]
public class PushedTilesetTests : IDisposable
{
    private const string PushedTile5 = """
         <tile id="5">
          <properties>
           <property name="achjAnimationName" value="Idle"/>
           <property name="achjSourceFile" value="hero.achx"/>
          </properties>
          <animation>
           <frame tileid="5" duration="100"/>
           <frame tileid="7" duration="250"/>
          </animation>
         </tile>

        """;

    private const string PushedTile9 = """
         <tile id="9">
          <properties>
           <property name="achjAnimationName" value="Run"/>
           <property name="achjSourceFile" value="hero.achx"/>
          </properties>
          <animation>
           <frame tileid="9" duration="100"/>
          </animation>
         </tile>

        """;

    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private readonly string _tsx;
    private readonly string _sync;

    public PushedTilesetTests()
    {
        _tsx = Path.Combine(_dir.Path, "tiles.tsx");
        _sync = Path.Combine(_dir.Path, "hero.tiledsync");
        Write(PushedTile5, PushedTile9);
        File.WriteAllText(_sync, """{ "TiledTilesetPaths": [ "tiles.tsx" ] }""");
    }

    public void Dispose() => _dir.Dispose();

    private void Write(params string[] tiles) => File.WriteAllText(_tsx, (
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="tiles" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="sheet.png" width="64" height="64"/>

        """ + string.Concat(tiles) + "</tileset>\n").ReplaceLineEndings("\n"));

    private AnimationChainSave Open(string name)
    {
        _ctx.ProjectManager.LoadTsxProject(new FilePath(_tsx));
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(chain => chain.Name == name);
    }

    [Fact]
    public void EditOfOneAnimation_SavesOnlyThatOne()
    {
        var idle = Open("Idle");
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Select(chain => chain.Name).ShouldBe(["Idle", "Run"]);
        string before = File.ReadAllText(_tsx);

        idle.Frames[1].FrameLength = 0.3f;
        _ctx.AppCommands.SaveCurrentAnimationChainList();

        File.ReadAllText(_tsx).ShouldBe(before.Replace("duration=\"250\"", "duration=\"300\""));
    }

    [Fact]
    public void Rename_StoresItAsName_KeepsTheTags_AndUndoRestoresTheFile()
    {
        var idle = Open("Idle");
        string before = File.ReadAllText(_tsx);

        _ctx.AppCommands.RenameChain(idle, "Walk");

        string[] added = File.ReadAllLines(_tsx).Except(before.Split('\n')).ToArray();
        added.ShouldHaveSingleItem().ShouldContain("<property name=\"Name\" value=\"Walk\"/>");
        File.ReadAllLines(_tsx).Length.ShouldBe(before.Split('\n').Length); // one line more, no empty last one

        _ctx.UndoManager.Undo();

        File.ReadAllText(_tsx).ShouldBe(before);
    }

    [Fact]
    public void NamePropertyOfTheUser_IsTheName()
    {
        Write(PushedTile5.Replace("<properties>", "<properties>\n<property name=\"Name\" value=\"Mine\"/>"));

        Open("Mine");
    }

    [Fact]
    public void Delete_KeepsTheTags()
    {
        _ctx.AppCommands.DeleteAnimationChains([Open("Idle")]);

        string text = File.ReadAllText(_tsx);
        text.ShouldNotContain("<frame tileid=\"5\"");
        text.ShouldContain("<property name=\"achjAnimationName\" value=\"Idle\"/>");
        text.ShouldContain(PushedTile9.ReplaceLineEndings("\n"));
    }

    // The tile still says Idle, so the new animation has to store its own name.
    [Fact]
    public void NewAnimationOnATileWhoseAnimationWasDeleted_KeepsItsName()
    {
        _ctx.AppCommands.DeleteAnimationChains([Open("Idle")]);
        var added = new AnimationChainSave { Name = "ID:5" };
        added.Frames.Add(new AnimationFrameSave
        {
            TextureName = "sheet.png", FrameLength = 0.1f, LeftCoordinate = 16, TopCoordinate = 16, RightCoordinate = 32, BottomCoordinate = 32,
        });
        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(added);
        _ctx.AppCommands.SaveCurrentAnimationChainList();

        Open("ID:5").Frames.Count.ShouldBe(1);
    }

    // A push wrote single tiles; the user can have grouped two of them in Tiled.
    [Fact]
    public void ParentId_MakesATileASatelliteOfAPushedOne()
    {
        Write(PushedTile5, PushedTile9.Replace("id=\"9\"", "id=\"6\"").Replace("<properties>", "<properties>\n<property name=\"ParentId\" type=\"int\" value=\"5\"/>"));

        var frame = Open("Idle").Frames[0];

        _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count.ShouldBe(1);
        (frame.LeftCoordinate, frame.RightCoordinate).ShouldBe((16, 48));
    }

    [Fact]
    public async Task TiledSync_SaveAndSaveAs_LeaveItAndTheTilesetAlone()
    {
        string achx = Path.Combine(_dir.Path, "hero.achx");
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = "Jump" });
        list.AnimationChains[0].Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        list.Save(achx);
        string tsx = File.ReadAllText(_tsx), sync = File.ReadAllText(_sync);
        _ctx.ProjectManager.LoadAnimationChain(new FilePath(achx));

        _ctx.AppCommands.RenameChain(_ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0], "Leap");
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(Path.Combine(_dir.Path, "copy.achj"));
        (await _ctx.AppCommands.SaveCurrentAnimationChainListAsync()).ShouldBeTrue();

        File.ReadAllText(achx).ShouldContain("Leap");
        File.ReadAllText(_tsx).ShouldBe(tsx);
        File.ReadAllText(_sync).ShouldBe(sync);
        Directory.GetFiles(_dir.Path, "*.tiledsync").ShouldBe([_sync]);
    }
}
