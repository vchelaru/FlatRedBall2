using AnimationEditor.Core.Data;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AETiledSyncSaveRoundTripTests
{
    private static string Serialize(AETiledSyncSave s)
    {
        var xs = new XmlSerializer(typeof(AETiledSyncSave));
        using var sw = new StringWriter();
        xs.Serialize(sw, s);
        return sw.ToString();
    }

    private static AETiledSyncSave Deserialize(string xml)
    {
        var xs = new XmlSerializer(typeof(AETiledSyncSave));
        using var sr = new StringReader(xml);
        return (AETiledSyncSave)xs.Deserialize(sr)!;
    }

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
