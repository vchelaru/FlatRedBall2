using AnimationEditor.Core;
using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// A tile's <c>Name</c> or <c>ParentId</c> property is the editor's only when the editor reads it: a non-empty
/// string <c>Name</c> on a chain's own tile, an int <c>ParentId</c> on an animated tile. Any other belongs to
/// the user and stays (issue #2014).
/// </summary>
public class TsxOwnPropertyTests : IDisposable
{
    private const string Name = "<property name=\"Name\" value=\"Gold chest\"/>";
    private const string IntName = "<property name=\"Name\" type=\"int\" value=\"7\"/>";
    private const string EmptyName = "<property name=\"Name\" value=\"\"/>";
    private const string StringParentId = "<property name=\"ParentId\" value=\"Chest\"/>";
    private const string IntParentId = "<property name=\"ParentId\" type=\"int\" value=\"0\"/>";
    private const string NegativeParentId = "<property name=\"ParentId\" type=\"int\" value=\"-1\"/>";

    private readonly TestHelpers.TempDir _dir = new();
    private readonly ProjectManager _pm = new();
    private string _path = "";

    public void Dispose() => _dir.Dispose();

    private static string Tile(int id, string properties, params int[] frames) =>
        $" <tile id=\"{id}\">\n"
        + (properties == "" ? "" : $"  <properties>\n   {properties}\n  </properties>\n")
        + (frames.Length == 0 ? "" : "  <animation>\n" + string.Concat(frames.Select(f => $"   <frame tileid=\"{f}\" duration=\"200\"/>\n")) + "  </animation>\n")
        + " </tile>\n";

    /// <summary>Opens a 4 by 4 tileset holding these tiles.</summary>
    private void Open(params string[] tiles)
    {
        _path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(_path,
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Heroes\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n"
            + " <image source=\"Heroes.png\" width=\"64\" height=\"64\"/>\n"
            + string.Concat(tiles) + "</tileset>\n");
        _pm.LoadTsxProject(new FilePath(_path));
    }

    private AnimationChainSave Chain(string name) => _pm.AnimationChainListSave!.AnimationChains.Single(c => c.Name == name);

    private void Delete(string chain) => _pm.AnimationChainListSave!.AnimationChains.Remove(Chain(chain));

    private static AnimationChainSave NewChain(string name, int tile, int widthInTiles = 1) => new()
    {
        Name = name,
        Frames =
        {
            new AnimationFrameSave
            {
                TextureName = "Heroes.png", FrameLength = 0.2f,
                LeftCoordinate = tile % 4 * 16, TopCoordinate = tile / 4 * 16,
                RightCoordinate = (tile % 4 + widthInTiles) * 16, BottomCoordinate = (tile / 4 + 1) * 16,
            },
        },
    };

    /// <summary>The saved tile's properties as written in the fixtures, or null without the tile.</summary>
    private string? SavedProperties(int tile)
    {
        var text = Regex.Match(File.ReadAllText(_path), $"<tile id=\"{tile}\"(?: ?/>|.*?</tile>)", RegexOptions.Singleline);
        return text.Success ? string.Concat(Regex.Matches(text.Value, "<property .*?/>").Select(m => m.Value.Replace(" />", "/>"))) : null;
    }

    [Theory]
    [InlineData(Name)]
    [InlineData(IntName)]
    [InlineData(EmptyName)]
    [InlineData(StringParentId)]
    [InlineData(IntParentId)]
    [InlineData(NegativeParentId)]
    public void SaveTsxProject_PropertyOnUnanimatedTile_IsKept(string property)
    {
        Open(Tile(0, "", 0, 1), Tile(5, property));
        var before = File.ReadAllText(_path);

        _pm.SaveTsxProject();
        File.ReadAllText(_path).ShouldBe(before, "a save with no edit");

        _pm.AnimationChainListSave!.AnimationChains[0].Frames[0].FrameLength = 0.3f;
        _pm.SaveTsxProject();
        SavedProperties(5).ShouldBe(property, "a save with an edit");
    }

    [Theory]
    [InlineData(Name)]
    [InlineData(IntName)]
    [InlineData(EmptyName)]
    [InlineData("<property name=\"Name\" value=\"ID:0\"/>")]
    [InlineData(StringParentId)]
    [InlineData(NegativeParentId)]
    public void SaveTsxProject_PropertyOnAnimatedTile_IsKept(string property)
    {
        Open(Tile(0, property, 0, 1));
        var before = File.ReadAllText(_path);

        _pm.SaveTsxProject();

        File.ReadAllText(_path).ShouldBe(before);
    }

    [Theory]
    [InlineData(IntName)]
    [InlineData(EmptyName)]
    [InlineData(StringParentId)]
    [InlineData(NegativeParentId)]
    public void SaveTsxProject_ChainDeleted_PropertyOfTheUserIsKept(string property)
    {
        Open(Tile(0, property, 0, 1));

        Delete("ID:0");
        _pm.SaveTsxProject();

        SavedProperties(0).ShouldBe(property);
        File.ReadAllText(_path).ShouldNotContain("<animation>");
    }

    /// <summary>On a chain's own tile the editor cannot tell a user's Name from one it wrote: it shows it as the chain's name.</summary>
    [Fact]
    public void SaveTsxProject_ChainRenamedOrDeleted_ItsOwnNameFollows()
    {
        Open(Tile(0, Name, 0, 1));

        Chain("Gold chest").Name = "Walk";
        _pm.SaveTsxProject();
        SavedProperties(0).ShouldBe("<property name=\"Name\" value=\"Walk\"/>");

        Delete("Walk");
        _pm.SaveTsxProject();
        SavedProperties(0).ShouldBeNull();
    }

    /// <summary>The name shown for a tile without one is not stored, so going back to it removes the property.</summary>
    [Fact]
    public void SaveTsxProject_ChainRenamedToItsAutomaticName_ItsOwnNameIsRemoved()
    {
        Open(Tile(0, Name, 0, 1));

        Chain("Gold chest").Name = "ID:0";
        _pm.SaveTsxProject();

        SavedProperties(0).ShouldBe("");
    }

    [Fact]
    public void SaveTsxProject_ChainWithAnEmptyNamePropertyRenamed_FillsItIn()
    {
        Open(Tile(0, EmptyName, 0, 1));

        Chain("ID:0").Name = "Walk";
        _pm.SaveTsxProject();

        SavedProperties(0).ShouldBe("<property name=\"Name\" value=\"Walk\"/>");
    }

    [Fact]
    public void SaveTsxProject_MultiTileChain_SatelliteKeepsTheUsersNameAndLosesOnlyItsParentId()
    {
        Open(Tile(0, "<property name=\"Name\" value=\"Walk\"/>", 0, 2), Tile(1, IntParentId + Name, 1, 3));
        var before = File.ReadAllText(_path);

        _pm.SaveTsxProject();
        File.ReadAllText(_path).ShouldBe(before, "a save with no edit");

        Delete("Walk");
        _pm.SaveTsxProject();
        SavedProperties(0).ShouldBeNull();
        SavedProperties(1).ShouldBe(Name);
    }

    [Fact]
    public void SaveTsxProject_NewMultiTileChainOverANamedTile_SatelliteKeepsTheUsersName()
    {
        Open(Tile(1, Name));

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Walk", tile: 0, widthInTiles: 2));
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(Name + IntParentId);
    }

    /// <summary>A chain's tile that joins another chain's group no longer has a chain name.</summary>
    [Fact]
    public void SaveTsxProject_ChainTileBecomesASatellite_ItsOwnNameIsRemoved()
    {
        Open(Tile(1, Name, 1, 3));

        Delete("Gold chest");
        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Walk", tile: 0, widthInTiles: 2));
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(IntParentId);
    }

    /// <summary>A tile holds one property of a name, so a chain that needs the user's is refused, not given it.</summary>
    [Theory]
    [InlineData(Name, 1, "Name", "Rename the animation to \"Gold chest\", use another tile, or undo.")]
    [InlineData(IntName, 1, "Name", "Rename the animation to \"ID:1\", use another tile, or undo.")]
    [InlineData(IntParentId, 1, "ParentId", "Use another tile, or undo.")]
    [InlineData(StringParentId, 0, "ParentId", "Use another tile, or undo.")]
    [InlineData(NegativeParentId, 0, "ParentId", "Use another tile, or undo.")]
    [InlineData("<property name=\"ParentId\" type=\"int\" value=\"9\"/>", 0, "ParentId", "Use another tile, or undo.")]
    public void SaveTsxProject_NewChainNeedsThePropertyOfTheUser_IsRefusedAndNothingIsWritten(string property, int chainTile, string expected, string way)
    {
        Open(Tile(1, property));
        var before = File.ReadAllText(_path);

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Walk", chainTile, widthInTiles: 2 - chainTile));

        var message = Should.Throw<InvalidOperationException>(() => _pm.SaveTsxProject()).Message;
        message.ShouldContain($"tile 1 has its own \"{expected}\" property");
        message.ShouldContain("\"Walk\"");
        message.ShouldEndWith(way);
        File.ReadAllText(_path).ShouldBe(before);
    }

    [Fact]
    public void SaveTsxProject_RenamedChainOnATileWithAnIntName_IsRefused()
    {
        Open(Tile(0, IntName, 0, 1));
        Chain("ID:0").Name = "Walk";

        Should.Throw<InvalidOperationException>(() => _pm.SaveTsxProject()).Message.ShouldContain("tile 0 has its own \"Name\" property");
        SavedProperties(0).ShouldBe(IntName);
    }

    [Fact]
    public void SaveTsxProject_NewChainNamedLikeTheTile_IsSaved()
    {
        Open(Tile(1, Name));

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Gold chest", tile: 1));
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(Name);
        File.ReadAllText(_path).ShouldContain("<animation>");
    }

    /// <summary>The refusal's advice, followed in the open document.</summary>
    [Theory]
    [InlineData(Name, "Gold chest")]
    [InlineData(IntName, "ID:1")]
    public void SaveTsxProject_RefusedChainRenamedAsAdvised_IsSaved(string property, string advised)
    {
        Open(Tile(1, property));
        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Walk", tile: 1));
        Should.Throw<InvalidOperationException>(() => _pm.SaveTsxProject());

        Chain("Walk").Name = advised;
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(property);
        File.ReadAllText(_path).ShouldContain("<animation>");
    }

    /// <summary>Whose a property is was settled when the file was opened, so a chain on the tile does not take it over.</summary>
    [Theory]
    [InlineData(Name, "Gold chest")]
    [InlineData(IntName, "ID:1")]
    [InlineData(EmptyName, "Walk")]
    [InlineData("<property name=\"Name\" value=\"ID:1\"/>", "Walk")]
    public void SaveTsxProject_ChainAddedSavedAndUndone_TileIsAsOpened(string property, string chain)
    {
        Open(Tile(1, property));

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain(chain, tile: 1));
        _pm.SaveTsxProject();
        _pm.SaveTsxProject();
        Delete(chain);
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(property);
        File.ReadAllText(_path).ShouldNotContain("<animation>");
    }

    [Fact]
    public void SaveTsxProject_MultiTileChainAddedSavedAndUndone_SatelliteIsAsOpened()
    {
        Open(Tile(1, IntParentId + Name));

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Walk", tile: 0, widthInTiles: 2));
        _pm.SaveTsxProject();
        Delete("Walk");
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(IntParentId + Name);
    }

    /// <summary>A satellite's own Name was not a chain's when the file was opened either.</summary>
    [Fact]
    public void SaveTsxProject_ChainOnAFormerSatelliteAddedSavedAndUndone_KeepsTheUsersName()
    {
        Open(Tile(0, "", 0, 2), Tile(1, IntParentId + Name, 1, 3));
        Delete("ID:0");
        _pm.SaveTsxProject();

        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Gold chest", tile: 1));
        _pm.SaveTsxProject();
        Delete("Gold chest");
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(Name);
    }

    [Theory]
    [InlineData(EmptyName)]
    [InlineData("<property name=\"Name\" value=\"ID:0\"/>")]
    public void SaveTsxProject_NameOfAnUnnamedChainFilledInAndUndone_TileIsAsOpened(string property)
    {
        Open(Tile(0, property, 0, 1));
        var before = File.ReadAllText(_path);

        Chain("ID:0").Name = "Walk";
        _pm.SaveTsxProject();
        Chain("Walk").Name = "ID:0";
        _pm.SaveTsxProject();

        File.ReadAllText(_path).Replace(" />", "/>").ShouldBe(before);
    }

    /// <summary>The other tab's document must not decide whose the properties of this one are.</summary>
    [Fact]
    public void RestoreTsxState_TabSwitchedAwayAndBack_TheUsersNameIsStillTheUsers()
    {
        Open(Tile(1, Name));
        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Gold chest", tile: 1));
        _pm.SaveTsxProject();

        var (model, state) = (_pm.AnimationChainListSave, _pm.CaptureTsxState());
        _pm.ResetToBlankDocument();
        (_pm.AnimationChainListSave, _pm.FileName) = (model, _path);
        _pm.RestoreTsxState(state);
        Delete("Gold chest");
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBe(Name);
    }

    /// <summary>The file cannot say who wrote the Name of an animated tile, so a reload makes it the chain's.</summary>
    [Fact]
    public void LoadTsxProject_AfterAChainTookTheSameName_TheNameIsTheChains()
    {
        Open(Tile(1, Name));
        _pm.AnimationChainListSave!.AnimationChains.Add(NewChain("Gold chest", tile: 1));
        _pm.SaveTsxProject();

        _pm.LoadTsxProject(new FilePath(_path));
        Delete("Gold chest");
        _pm.SaveTsxProject();

        SavedProperties(1).ShouldBeNull();
    }

    /// <summary>Not valid in Tiled, but a file can hold it: the editor reads the string one.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SaveTsxProject_TileWithAnIntAndAStringName_TheStringIsTheChainsAndTheIntStays(bool intFirst)
    {
        static string Both(bool intFirst, string name) => intFirst ? IntName + name : name + IntName;
        Open(Tile(0, Both(intFirst, Name), 0, 1));
        var before = File.ReadAllText(_path);

        _pm.SaveTsxProject();
        File.ReadAllText(_path).ShouldBe(before);

        Chain("Gold chest").Name = "Walk";
        _pm.SaveTsxProject();
        SavedProperties(0).ShouldBe(Both(intFirst, "<property name=\"Name\" value=\"Walk\"/>"));

        Delete("Walk");
        _pm.SaveTsxProject();
        SavedProperties(0).ShouldBe(IntName);
    }

    /// <summary>The Name of a tile with no animation is not a chain's, so it clashes with none.</summary>
    [Fact]
    public void GetChainNamesWithTsxIssues_UnanimatedTileSharesAChainsName_IsNotAnIssue()
    {
        Open(Tile(0, Name, 0, 1), Tile(5, Name));

        _pm.GetChainNamesWithTsxIssues().ShouldBeEmpty();
    }

    /// <summary>The push has its own two property names and never reads or writes these.</summary>
    [Theory]
    [InlineData(Name)]
    [InlineData(IntName)]
    [InlineData(EmptyName)]
    [InlineData(StringParentId)]
    [InlineData(IntParentId)]
    public void TiledTilesetSync_PushThenChainRemoved_KeepsThePropertyOfTheUser(string property)
    {
        Open(Tile(0, property), Tile(5, property));
        var achj = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        achj.AnimationChains.Add(NewChain("Walk", tile: 0));

        TiledTilesetSyncRunner.SyncAll(achj, Path.Combine(_dir.Path, "Hero.achx"), [_path]);
        SavedProperties(0)!.ShouldStartWith(property);
        SavedProperties(0)!.ShouldContain("achjSourceFile");
        SavedProperties(5).ShouldBe(property);

        _pm.LoadTsxProject(new FilePath(_path));
        _pm.SaveTsxProject();
        SavedProperties(0)!.ShouldStartWith(property);
        SavedProperties(5).ShouldBe(property);

        achj.AnimationChains.Clear();
        TiledTilesetSyncRunner.SyncAll(achj, Path.Combine(_dir.Path, "Hero.achx"), [_path]);
        SavedProperties(0).ShouldBe(property);
        SavedProperties(5).ShouldBe(property);
    }
}
