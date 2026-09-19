using AnimationEditor.Core.Data;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AETiledSyncSaveRoundTripTests
{
    private static string Serialize(AETiledSyncSave s) =>
        JsonSerializer.Serialize(s, AETiledSyncJsonContext.Default.AETiledSyncSave);

    private static AETiledSyncSave Deserialize(string json) =>
        JsonSerializer.Deserialize(json, AETiledSyncJsonContext.Default.AETiledSyncSave)!;

    [Fact]
    public void TiledTilesetPaths_RoundTrip_PreservesAllPathsAndOrder()
    {
        var s = new AETiledSyncSave();
        s.TiledTilesetPaths.Add("../Tilesets/Heroes.tsx");
        s.TiledTilesetPaths.Add("../Tilesets/Enemies.tsx");

        var loaded = Deserialize(Serialize(s));

        Assert.Equal(["../Tilesets/Heroes.tsx", "../Tilesets/Enemies.tsx"], loaded.TiledTilesetPaths);
    }

    [Fact]
    public void EmptySettings_RoundTrip_TiledTilesetPathsIsEmpty()
    {
        var loaded = Deserialize(Serialize(new AETiledSyncSave()));

        Assert.Empty(loaded.TiledTilesetPaths);
    }
}
