using System.Text;
using FlatRedBall.AnimationChain;
using FlatRedBall2.AnimationEditorCommon;
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
        // AnimationEditorCommon's FromFile stores FileName verbatim (not resolved to an absolute
        // path) -- AchxLoader.Load resolves only rooted paths, see ContentFile.ReadSave.
        var save = AnimationChainListSave.FromFile("my/path/anim.achx", XmlStream(SimpleAchx));
        Assert.Equal("my/path/anim.achx", save.FileName);
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
        var list = new AnimationChainList<AnimationFrame>();
        Assert.Null(list["NotHere"]);
    }

    // ─── AnimationChainList stream reload ─────────────────────────────────────────

    [Fact]
    public void TryReloadFrom_Stream_ReplacesExistingFramesAndAddsNewChains()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);

        const string reloadXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Run</Name>
                <Frame>
                  <TextureName>tex.png</TextureName>
                  <FrameLength>0.25</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
              <AnimationChain>
                <Name>Slide</Name>
                <Frame>
                  <TextureName>tex.png</TextureName>
                  <FrameLength>0.5</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;

        using var reloadStream = new MemoryStream(Encoding.UTF8.GetBytes(reloadXml));
        var ok = list.TryReload(reloadStream, _ => null);

        Assert.True(ok);
        Assert.Single(list["Run"]!);
        Assert.Equal(TimeSpan.FromSeconds(0.25), list["Run"]![0].FrameLength);
        Assert.NotNull(list["Slide"]);
    }

    [Fact]
    public void TryReloadFrom_Stream_InvalidXml_ReturnsFalseAndLeavesListUntouched()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);
        var beforeRunFrames = list["Run"]!.Count;

        const string invalidXml = "<AnimationChainArraySave><AnimationChain>";
        using var reloadStream = new MemoryStream(Encoding.UTF8.GetBytes(invalidXml));
        var ok = list.TryReload(reloadStream, _ => null);

        Assert.False(ok);
        Assert.Equal(beforeRunFrames, list["Run"]!.Count);
        Assert.Null(list["Slide"]);
    }

    [Fact]
    public void TryReloadFrom_Stream_JsonContent_AutoDetectsAndReplacesFrames()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);

        var reloadSave = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "tex.png", FrameLength = 0.25f });
        reloadSave.AnimationChains.Add(chain);

        using var reloadStream = new MemoryStream(Encoding.UTF8.GetBytes(reloadSave.ToJsonString()));
        var ok = list.TryReload(reloadStream, _ => null);

        Assert.True(ok);
        Assert.Single(list["Run"]!);
        Assert.Equal(TimeSpan.FromSeconds(0.25), list["Run"]![0].FrameLength);
    }

    [Fact]
    public void TryReloadFrom_Stream_InvalidJsonContent_ReturnsFalseAndLeavesListUntouched()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);
        var beforeRunFrames = list["Run"]!.Count;

        using var reloadStream = new MemoryStream(Encoding.UTF8.GetBytes("{not valid json"));
        var ok = list.TryReload(reloadStream, _ => null);

        Assert.False(ok);
        Assert.Equal(beforeRunFrames, list["Run"]!.Count);
    }

    // ─── AnimationChainList.TryReload(path) — .achj (JSON) dispatch by extension ─────────

    [Fact]
    public void TryReloadFrom_Path_AchjExtension_ParsesJsonAndReplacesFrames()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);

        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "tex.png", FrameLength = 0.25f });
        save.AnimationChains.Add(chain);

        var tempPath = Path.GetTempFileName() + ".achj";
        try
        {
            save.SaveJson(tempPath);

            var ok = list.TryReload(tempPath, _ => null);

            Assert.True(ok);
            Assert.Single(list["Run"]!);
            Assert.Equal(TimeSpan.FromSeconds(0.25), list["Run"]![0].FrameLength);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void TryReloadFrom_Path_InvalidJson_ReturnsFalseAndLeavesListUntouched()
    {
        var list = AnimationChainListSave.FromFile("dummy.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(_ => null);
        var beforeRunFrames = list["Run"]!.Count;

        var tempPath = Path.GetTempFileName() + ".achj";
        try
        {
            File.WriteAllText(tempPath, "not valid json at all");

            var ok = list.TryReload(tempPath, _ => null);

            Assert.False(ok);
            Assert.Equal(beforeRunFrames, list["Run"]!.Count);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ─── FromStream ──────────────────────────────────────────────────────────────

    [Fact]
    public void FromStream_ParsesChainNames()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleAchx));
        var save = AnimationChainListSave.FromStream(stream);
        Assert.Equal(2, save.AnimationChains.Count);
        Assert.Equal("Run", save.AnimationChains[0].Name);
        Assert.Equal("Idle", save.AnimationChains[1].Name);
    }

    [Fact]
    public void FromStream_FileNameIsEmpty()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleAchx));
        var save = AnimationChainListSave.FromStream(stream);
        Assert.Equal(string.Empty, save.FileName);
    }

    [Fact]
    public void FromStream_ParsesFrameCoordinates()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleAchx));
        var save = AnimationChainListSave.FromStream(stream);
        var f = save.AnimationChains[0].Frames[0];
        Assert.Equal(0f,   f.LeftCoordinate,   precision: 5);
        Assert.Equal(0.5f, f.RightCoordinate,  precision: 5);
        Assert.Equal(0f,   f.TopCoordinate,    precision: 5);
        Assert.Equal(1f,   f.BottomCoordinate, precision: 5);
    }

    [Fact]
    public void FromStream_ParsesFrameLength_Milliseconds()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MillisecondAchx));
        var save = AnimationChainListSave.FromStream(stream);
        Assert.Equal(100f, save.AnimationChains[0].Frames[0].FrameLength, precision: 5);
    }

    // ─── FromString ──────────────────────────────────────────────────────────────

    [Fact]
    public void FromString_ParsesChainNames()
    {
        var save = AnimationChainListSave.FromString(SimpleAchx);
        Assert.Equal(2, save.AnimationChains.Count);
        Assert.Equal("Run", save.AnimationChains[0].Name);
        Assert.Equal("Idle", save.AnimationChains[1].Name);
    }

    [Fact]
    public void FromString_FileNameIsEmpty()
    {
        var save = AnimationChainListSave.FromString(SimpleAchx);
        Assert.Equal(string.Empty, save.FileName);
    }

    [Fact]
    public void FromString_ParsesFrameCoordinates()
    {
        var save = AnimationChainListSave.FromString(SimpleAchx);
        var f = save.AnimationChains[0].Frames[0];
        Assert.Equal(0f,   f.LeftCoordinate,   precision: 5);
        Assert.Equal(0.5f, f.RightCoordinate,  precision: 5);
        Assert.Equal(0f,   f.TopCoordinate,    precision: 5);
        Assert.Equal(1f,   f.BottomCoordinate, precision: 5);
    }

    [Fact]
    public void FromString_ParsesFrameCount()
    {
        var save = AnimationChainListSave.FromString(SimpleAchx);
        Assert.Equal(2, save.AnimationChains[0].Frames.Count);
        Assert.Single(save.AnimationChains[1].Frames);
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

    // ─── ContentFile (title-relative vs. rooted path routing, #1227) ──────────────

    [Fact]
    public void ReadSave_RelativeAchxPath_TexturePathStaysRelative()
    {
        // A cwd-based absolute path here would make TitleContainer.OpenStream throw.
        var requested = new List<string>();

        ContentFile.ReadSave("Content/anim.achx", XmlStream(SimpleAchx))
            .ToAnimationChainList(p => { requested.Add(p); return null; });

        Assert.Equal(Path.Combine("Content", "player.png"), requested[0]);
    }

    [Fact]
    public void ReadSave_RootedAchxPath_TexturePathIsRootedNextToAchx()
    {
        string dir = Path.Combine(Path.GetTempPath(), "game");
        var requested = new List<string>();

        ContentFile.ReadSave(Path.Combine(dir, "anim.achx"), XmlStream(SimpleAchx))
            .ToAnimationChainList(p => { requested.Add(p); return null; });

        Assert.Equal(Path.Combine(dir, "player.png"), requested[0]);
    }

    [Fact]
    public void TryOpen_RelativePath_ReadsThroughTitleContainer()
    {
        string? titlePath = null;

        using var stream = ContentFile.TryOpen("Content/player.png",
            openTitle: p => { titlePath = p; return new MemoryStream(); },
            openFile: _ => throw new InvalidOperationException("file system must not be used"));

        Assert.Equal("Content/player.png", titlePath);
    }

    [Fact]
    public void TryOpen_RootedPath_ReadsFromFileSystem()
    {
        string rooted = Path.Combine(Path.GetTempPath(), "player.png");
        string? filePath = null;

        using var stream = ContentFile.TryOpen(rooted,
            openTitle: _ => throw new InvalidOperationException("TitleContainer rejects rooted paths"),
            openFile: p => { filePath = p; return new MemoryStream(); });

        Assert.Equal(rooted, filePath);
    }

    [Fact]
    public void TryOpen_MissingTitleFile_ReturnsNull()
    {
        var stream = ContentFile.TryOpen("Content/missing.png",
            openTitle: _ => throw new FileNotFoundException(),
            openFile: _ => throw new InvalidOperationException());

        Assert.Null(stream);
    }
}
