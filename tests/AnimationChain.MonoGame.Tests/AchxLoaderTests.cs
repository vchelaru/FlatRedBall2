using System.Text;
using FlatRedBall.AnimationChain.Content;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class AchxLoaderTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static Func<string, Stream> XmlStream(string xml) =>
        _ => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    // Minimal valid .achx XML (UV coordinates, seconds)
    private const string SimpleAchx = """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>UV</CoordinateType>
          <AnimationChain>
            <Name>Run</Name>
            <Frame>
              <TextureName>player.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate>
              <RightCoordinate>0.5</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>1</BottomCoordinate>
            </Frame>
            <Frame>
              <TextureName>player.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0.5</LeftCoordinate>
              <RightCoordinate>1</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>1</BottomCoordinate>
            </Frame>
          </AnimationChain>
          <AnimationChain>
            <Name>Idle</Name>
            <Frame>
              <TextureName>player.png</TextureName>
              <FrameLength>0.5</FrameLength>
              <LeftCoordinate>0</LeftCoordinate>
              <RightCoordinate>1</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>1</BottomCoordinate>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    private const string MillisecondAchx = """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>false</FileRelativeTextures>
          <TimeMeasurementUnit>Millisecond</TimeMeasurementUnit>
          <CoordinateType>UV</CoordinateType>
          <AnimationChain>
            <Name>Run</Name>
            <Frame>
              <TextureName>tex.png</TextureName>
              <FrameLength>100</FrameLength>
              <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    // ─── AnimationChainListSave.FromFile ─────────────────────────────────────────

    [Fact]
    public void FromFile_ParsesChainNames()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        Assert.Equal(2, save.AnimationChains.Count);
        Assert.Equal("Run", save.AnimationChains[0].Name);
        Assert.Equal("Idle", save.AnimationChains[1].Name);
    }

    [Fact]
    public void FromFile_ParsesFrameCount()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        Assert.Equal(2, save.AnimationChains[0].Frames.Count);
        Assert.Single(save.AnimationChains[1].Frames);
    }

    [Fact]
    public void FromFile_ParsesFrameLength_Seconds()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        Assert.Equal(0.1f, save.AnimationChains[0].Frames[0].FrameLength, precision: 5);
    }

    [Fact]
    public void FromFile_ParsesFrameLength_Milliseconds()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(MillisecondAchx));
        Assert.Equal(100f, save.AnimationChains[0].Frames[0].FrameLength, precision: 5);
        // Converted to seconds in ToAnimationChainList, not in FromFile
    }

    [Fact]
    public void FromFile_ParsesTextureCoordinates()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var f = save.AnimationChains[0].Frames[0];
        Assert.Equal(0f,   f.LeftCoordinate,   precision: 5);
        Assert.Equal(0.5f, f.RightCoordinate,  precision: 5);
        Assert.Equal(0f,   f.TopCoordinate,    precision: 5);
        Assert.Equal(1f,   f.BottomCoordinate, precision: 5);
    }

    [Fact]
    public void FromFile_SetsFileName()
    {
        var save = AnimationChainListSave.FromFile("my/path/anim.achx", XmlStream(SimpleAchx));
        // FileName is always stored as an absolute path to avoid double-resolution
        // when callers combine it with their own achxDir.
        Assert.Equal(Path.GetFullPath("my/path/anim.achx"), save.FileName);
    }

    // ─── ToAnimationChainList ────────────────────────────────────────────────────

    [Fact]
    public void ToAnimationChainList_NullTextureLoader_FramesHaveNullTexture()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var list = save.ToAnimationChainList(_ => null);
        Assert.All(list.SelectMany(c => c), f => Assert.Null(f.Texture));
    }

    [Fact]
    public void ToAnimationChainList_MillisecondUnit_ConvertsToSeconds()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(MillisecondAchx));
        var list = save.ToAnimationChainList(_ => null);
        Assert.Equal(TimeSpan.FromSeconds(0.1), list["Run"]![0].FrameLength);
    }

    [Fact]
    public void ToAnimationChainList_ChainNamePreserved()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var list = save.ToAnimationChainList(_ => null);
        Assert.NotNull(list["Run"]);
        Assert.NotNull(list["Idle"]);
    }

    [Fact]
    public void ToAnimationChainList_RepeatedTextureName_CalledOncePerDistinctName()
    {
        // Both "Run" frames reference the same texture name "player.png".
        // A caching loader should only call the loader once per distinct name.
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var callPaths = new List<string>();
        save.ToAnimationChainList(path =>
        {
            callPaths.Add(path);
            return null;
        });
        // Note: ToAnimationChainList itself does NOT cache — that's AchxLoader's job.
        // This test verifies the raw count so we know the contract.
        Assert.Equal(3, callPaths.Count); // 2 Run frames + 1 Idle frame, all "player.png"
    }

    [Fact]
    public void ToAnimationChainList_FlipFlags_Preserved()
    {
        const string flipAchx = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Flip</Name>
                <Frame>
                  <FlipHorizontal>true</FlipHorizontal>
                  <FlipVertical>true</FlipVertical>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(flipAchx));
        var list = save.ToAnimationChainList(_ => null);
        var f = list["Flip"]![0];
        Assert.True(f.FlipHorizontal);
        Assert.True(f.FlipVertical);
    }

    [Fact]
    public void ToAnimationChainList_RelativeXY_Preserved()
    {
        const string offsetAchx = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Kick</Name>
                <Frame>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                  <RelativeX>5</RelativeX>
                  <RelativeY>-3</RelativeY>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(offsetAchx));
        var list = save.ToAnimationChainList(_ => null);
        var f = list["Kick"]![0];
        Assert.Equal(5f,  f.RelativeX, precision: 5);
        Assert.Equal(-3f, f.RelativeY, precision: 5);
    }

    // ─── AnimationChainList string indexer ───────────────────────────────────────

    [Fact]
    public void Indexer_MissingName_ReturnsNull()
    {
        var list = new FlatRedBall.AnimationChain.AnimationChainList();
        Assert.Null(list["NotHere"]);
    }

    // ─── Save / round-trip ────────────────────────────────────────────────────────

    [Fact]
    public void Save_RoundTrip_PreservesChainNames()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var tmpPath = Path.GetTempFileName() + ".achx";
        try
        {
            save.Save(tmpPath);
            var reloaded = AnimationChainListSave.FromFile(tmpPath);
            Assert.Equal(
                save.AnimationChains.Select(c => c.Name),
                reloaded.AnimationChains.Select(c => c.Name));
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    [Fact]
    public void Save_RoundTrip_PreservesFrameCoordinates()
    {
        var save = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx));
        var tmpPath = Path.GetTempFileName() + ".achx";
        try
        {
            save.Save(tmpPath);
            var reloaded = AnimationChainListSave.FromFile(tmpPath);
            var orig    = save.AnimationChains[0].Frames[0];
            var roundtrip = reloaded.AnimationChains[0].Frames[0];
            Assert.Equal(orig.LeftCoordinate,   roundtrip.LeftCoordinate,   precision: 5);
            Assert.Equal(orig.RightCoordinate,  roundtrip.RightCoordinate,  precision: 5);
            Assert.Equal(orig.TopCoordinate,    roundtrip.TopCoordinate,    precision: 5);
            Assert.Equal(orig.BottomCoordinate, roundtrip.BottomCoordinate, precision: 5);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }
}
