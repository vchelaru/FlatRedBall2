using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Covers AnimationChainList.TryReloadFrom — the in-place reload path for .achx edits during
// hot-reload. Design intent: preserve chain-instance identity by name so any live
// Sprite.CurrentAnimation reference keeps playing from the updated frames without resetting.
public class AnimationChainListReloadTests
{
    // In-memory file system: path → XML bytes. Routed through ContentLoader.StreamProvider
    // so the engine never touches the disk and matches the production WASM/desktop code path
    // (TitleContainer.OpenStream rejects absolute filesystem paths).
    private readonly Dictionary<string, byte[]> _virtualFiles = new(StringComparer.OrdinalIgnoreCase);

    private ContentLoader MakeContent()
    {
        var svc = new ContentLoader();
        svc.TextureLoader = _ => null!; // frames tolerate null Texture; we only validate chain structure
        svc.StreamProvider = path =>
        {
            if (!_virtualFiles.TryGetValue(path, out var bytes))
                throw new FileNotFoundException(path);
            return new MemoryStream(bytes);
        };
        return svc;
    }

    private string WriteAchx(string fileName, params (string ChainName, int FrameCount)[] chains)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<AnimationChainArraySave>");
        sb.AppendLine("  <CoordinateType>Pixel</CoordinateType>");
        foreach (var (name, count) in chains)
        {
            sb.AppendLine("  <AnimationChain>");
            sb.AppendLine($"    <Name>{name}</Name>");
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine("    <Frame>");
                sb.AppendLine($"      <TextureName>{name}.png</TextureName>");
                sb.AppendLine($"      <FrameLength>0.1</FrameLength>");
                sb.AppendLine($"      <LeftCoordinate>{i * 16}</LeftCoordinate>");
                sb.AppendLine($"      <RightCoordinate>{i * 16 + 16}</RightCoordinate>");
                sb.AppendLine($"      <TopCoordinate>0</TopCoordinate>");
                sb.AppendLine($"      <BottomCoordinate>16</BottomCoordinate>");
                sb.AppendLine("    </Frame>");
            }
            sb.AppendLine("  </AnimationChain>");
        }
        sb.AppendLine("</AnimationChainArraySave>");

        _virtualFiles[fileName] = Encoding.UTF8.GetBytes(sb.ToString());
        return fileName;
    }

    private void WriteRaw(string fileName, string contents)
        => _virtualFiles[fileName] = Encoding.UTF8.GetBytes(contents);

    private void DeleteFile(string fileName) => _virtualFiles.Remove(fileName);

    private AnimationChainList LoadFresh(string path, ContentLoader content)
        => content.LoadAnimationChainList(path);

    [Fact]
    public void TryReloadFrom_MissingFile_ReturnsFalse()
    {
        var content = MakeContent();
        var list = LoadFresh(WriteAchx("base.achx", ("Walk", 2)), content);

        var result = list.TryReloadFrom("does-not-exist.achx", content);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryReloadFrom_InvalidXml_ReturnsFalse()
    {
        var content = MakeContent();
        var list = LoadFresh(WriteAchx("base.achx", ("Walk", 2)), content);
        WriteRaw("bad.achx", "not valid xml at all");

        var result = list.TryReloadFrom("bad.achx", content);

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryReloadFrom_InvalidJson_ReturnsFalse()
    {
        var content = MakeContent();
        var list = LoadFresh(WriteAchx("base.achx", ("Walk", 2)), content);
        WriteRaw("bad.achj", "not valid json at all");

        var result = list.TryReloadFrom("bad.achj", content);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("bad.achx", "<AnimationChainArraySave><AnimationChain><Loop>fals</Loop></AnimationChain></AnimationChainArraySave>")]
    [InlineData("bad.achj", "{\"animationChains\":[null]}")]
    public void TryReloadFrom_ValueThatDoesNotParse_ReturnsFalse(string path, string text)
    {
        var content = MakeContent();
        var list = LoadFresh(WriteAchx("base.achx", ("Walk", 2)), content);
        WriteRaw(path, text);

        list.TryReloadFrom(path, content).ShouldBeFalse();
    }

    [Fact]
    public void TryReloadFrom_AchjFile_SameChain_PreservesChainInstanceIdentity()
    {
        var content = MakeContent();
        var path = "anim.achj";
        WriteRaw(path, MakeAchjJson(("Walk", 2)));
        var list = LoadFresh(path, content);
        var walkBefore = list["Walk"];

        // Author edits the file — same chain name, grown frame count.
        WriteRaw(path, MakeAchjJson(("Walk", 4)));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list["Walk"].ShouldBeSameAs(walkBefore); // identity preserved — live Sprite refs still valid
        list["Walk"]!.Count.ShouldBe(4);
    }

    private static string MakeAchjJson(params (string ChainName, int FrameCount)[] chains)
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var (name, count) in chains)
        {
            var chain = new AnimationChainSave { Name = name };
            for (int i = 0; i < count; i++)
                chain.Frames.Add(new AnimationFrameSave
                {
                    TextureName = $"{name}.png",
                    FrameLength = 0.1f,
                    LeftCoordinate = i * 16,
                    RightCoordinate = i * 16 + 16,
                    TopCoordinate = 0,
                    BottomCoordinate = 16,
                });
            save.AnimationChains.Add(chain);
        }
        return save.ToJsonString();
    }

    [Fact]
    public void TryReloadFrom_NonParseInvalidOperationException_Propagates()
    {
        // A genuine bug (not file mid-write / bad XML) should surface rather than be swallowed as
        // "parse failure." XmlSerializer-wrapped parse errors carry an XmlException inner; anything
        // else means something real broke and silent-false would hide the regression.
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 1));
        var list = LoadFresh(path, content);

        // Add a chain referencing a new texture name so the reload hits the loader (cached
        // textures bypass it). Break the loader so any fresh load throws.
        WriteAchx("anim.achx", ("Walk", 1), ("NewChain", 1));
        content.TextureLoader = _ => throw new System.InvalidOperationException("simulated downstream failure");

        Should.Throw<System.InvalidOperationException>(() => list.TryReloadFrom(path, content));
    }

    [Fact]
    public void TryReloadFrom_SameChain_PreservesChainInstanceIdentity()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 2));
        var list = LoadFresh(path, content);
        var walkBefore = list["Walk"];

        // Author edits the file — same chain name, same frame count.
        WriteAchx("anim.achx", ("Walk", 2));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list["Walk"].ShouldBeSameAs(walkBefore); // identity preserved — live Sprite refs still valid
    }

    [Fact]
    public void TryReloadFrom_SameChain_ShortenedFrameList_ReducesInPlace()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 5));
        var list = LoadFresh(path, content);
        list["Walk"]!.Count.ShouldBe(5);

        WriteAchx("anim.achx", ("Walk", 2));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list["Walk"]!.Count.ShouldBe(2);
    }

    [Fact]
    public void TryReloadFrom_SameChain_LongerFrameList_GrowsInPlace()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 2));
        var list = LoadFresh(path, content);

        WriteAchx("anim.achx", ("Walk", 6));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list["Walk"]!.Count.ShouldBe(6);
    }

    [Fact]
    public void TryReloadFrom_TwoChainsShareAName_EachTakesItsOwnFramesAndFlags()
    {
        var content = MakeContent();
        var list = LoadFresh(WriteAchx("anim.achx", ("Walk", 1), ("Walk", 2)), content);
        const string frame = "<Frame><FrameLength>0.1</FrameLength></Frame>";
        WriteRaw("anim.achx", $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name>{frame}</AnimationChain>"
            + $"<AnimationChain><Name>Walk</Name><Loop>false</Loop><PingPong>true</PingPong><Reverse>true</Reverse>{frame}{frame}{frame}</AnimationChain></AnimationChainArraySave>");

        list.TryReloadFrom("anim.achx", content).ShouldBeTrue();

        list.ConvertAll(c => (c.Count, c.Loop, c.PingPong, c.Reversed)).ShouldBe([(1, true, false, false), (3, false, true, true)]);
    }

    private const string OneFrame = "<Frame><FrameLength>0.1</FrameLength></Frame>";

    private static string WalkFile(string flags) =>
        $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name>{flags}{OneFrame}</AnimationChain></AnimationChainArraySave>";

    // A sprite on the current screen playing Walk, which the engine's or the screen's own loader reloads.
    private static (AnimationChainList List, Sprite Sprite, Action<string> Reload) PlayingOnScreen(bool screenLoader)
    {
        var engine = new FlatRedBallService();
        string file = WalkFile("");
        var content = screenLoader ? engine.CurrentScreen.ContentLoader : engine.Content;
        content.StreamProvider = _ => new MemoryStream(Encoding.UTF8.GetBytes(file));
        var list = content.LoadAnimationChainList("a.achx");
        var sprite = new Sprite { AnimationChains = list };
        sprite.PlayAnimation("Walk");
        engine.CurrentScreen.Add(sprite);
        return (list, sprite, text =>
        {
            file = text;
            list.TryReloadFrom("a.achx", content).ShouldBeTrue();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryReloadFrom_FlagsChangedInFile_PlayingSpriteTakesThem(bool screenLoader)
    {
        var (_, sprite, reload) = PlayingOnScreen(screenLoader);

        reload(WalkFile("<Loop>false</Loop><PingPong>true</PingPong><Reverse>true</Reverse>"));

        (sprite.IsLooping, sprite.IsPingPong, sprite.IsReversed).ShouldBe((false, true, true));
    }

    [Fact]
    public void TryReloadFrom_FlagNotChangedInFile_GameOverrideStays()
    {
        var (_, sprite, reload) = PlayingOnScreen(screenLoader: false);
        sprite.IsLooping = false;
        sprite.IsReversed = true;

        reload(WalkFile("<PingPong>true</PingPong>"));

        (sprite.IsLooping, sprite.IsPingPong, sprite.IsReversed).ShouldBe((false, true, true));
    }

    [Fact]
    public void TryReloadFrom_FlagsChangedInFile_SpritePlayingAnotherChainKeepsItsOwn()
    {
        var (list, sprite, reload) = PlayingOnScreen(screenLoader: false);
        var other = new AnimationChain { Name = "Other" };
        list.Add(other);
        sprite.PlayAnimation(other);

        reload(WalkFile("<Loop>false</Loop>"));

        sprite.IsLooping.ShouldBeTrue();
    }

    [Fact]
    public void TryReloadFrom_GameSetTheChainsFlag_ChainAndPlayingSpriteTakeTheFiles()
    {
        var (list, sprite, reload) = PlayingOnScreen(screenLoader: false);
        list[0].Loop = false;
        sprite.IsLooping = false;

        reload(WalkFile(""));

        (list[0].Loop, sprite.IsLooping).ShouldBe((true, true));
    }

    [Fact]
    public void TryReloadFrom_LoopTurnedOn_FireAndForgetEffectStillPlaysOnce()
    {
        var engine = new FlatRedBallService();
        string file = WalkFile("<Loop>false</Loop>");
        engine.Content.StreamProvider = _ => new MemoryStream(Encoding.UTF8.GetBytes(file));
        var list = engine.Content.LoadAnimationChainList("a.achx");
        var screen = engine.CurrentScreen;
        screen.Engine = engine;
        var effect = screen.CreateFireAndForget(list, "Walk", x: 0, y: 0);

        file = WalkFile("<Loop>true</Loop>");
        list.TryReloadFrom("a.achx", engine.Content).ShouldBeTrue();
        screen.Update(new FrameTime(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5), TimeSpan.Zero, TimeSpan.Zero));

        screen.Entities.ShouldNotContain(effect);
    }

    [Theory]
    [InlineData("<PingPong>true</PingPong>", "", 3)]
    [InlineData("", "", 1)]
    [InlineData("<Reverse>true</Reverse>", "", 1)]
    public void TryReloadFrom_InsideAFrameEventHandler_DoesNotThrow(string flagsBefore, string flagsAfter, int framesAfter)
    {
        static string File(string flags, int frames)
        {
            const string frame = "<Frame><FrameLength>0.1</FrameLength><Events><Event><Name>step</Name></Event></Events></Frame>";
            return $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name>{flags}{string.Concat(System.Linq.Enumerable.Repeat(frame, frames))}</AnimationChain></AnimationChainArraySave>";
        }
        var engine = new FlatRedBallService();
        string file = File(flagsBefore, 3);
        engine.Content.StreamProvider = _ => new MemoryStream(Encoding.UTF8.GetBytes(file));
        var list = engine.Content.LoadAnimationChainList("a.achx");
        var sprite = new Sprite { AnimationChains = list };
        engine.CurrentScreen.Add(sprite);
        sprite.PlayAnimation("Walk");
        int raised = 0;
        sprite.FrameEventRaised += _ =>
        {
            raised++;
            file = File(flagsAfter, framesAfter);
            list.TryReloadFrom("a.achx", engine.Content).ShouldBeTrue();
        };

        Should.NotThrow(() => sprite.AnimateSelf(0.45));
        Should.NotThrow(() => sprite.AnimateSelf(0.45));

        raised.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Loop_ChangedWithoutAReload_PlayingSpriteKeepsItsValue()
    {
        var (list, sprite, _) = PlayingOnScreen(screenLoader: false);

        list[0].Loop = false;

        sprite.IsLooping.ShouldBeTrue();
    }

    [Fact]
    public void TryReloadFrom_NewChainInSource_AppendedToList()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 2));
        var list = LoadFresh(path, content);

        WriteAchx("anim.achx", ("Walk", 2), ("Run", 3));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list.Count.ShouldBe(2);
        list["Run"].ShouldNotBeNull();
        list["Run"]!.Count.ShouldBe(3);
    }

    [Fact]
    public void TryReloadFrom_ChainRemovedFromSource_OrphanedChainRemains()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 2), ("Run", 3));
        var list = LoadFresh(path, content);
        var runBefore = list["Run"];

        // Author removes "Run" from the file.
        WriteAchx("anim.achx", ("Walk", 2));

        list.TryReloadFrom(path, content).ShouldBeTrue();

        // Orphaned chain still present — live sprites playing Run keep rendering their old art
        // rather than snapping to black / throwing.
        list.Count.ShouldBe(2);
        list["Run"].ShouldBeSameAs(runBefore);
    }

    [Fact]
    public void TryReloadFrom_FrameCoordinateChange_NewRectangleAppliedToExistingChain()
    {
        var content = MakeContent();
        var path = WriteAchx("anim.achx", ("Walk", 1));
        var list = LoadFresh(path, content);
        var walkBefore = list["Walk"];

        // Rewrite with different coordinates (simulate author moving the frame in the atlas).
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<AnimationChainArraySave>");
        sb.AppendLine("  <CoordinateType>Pixel</CoordinateType>");
        sb.AppendLine("  <AnimationChain>");
        sb.AppendLine("    <Name>Walk</Name>");
        sb.AppendLine("    <Frame>");
        sb.AppendLine("      <TextureName>Walk.png</TextureName>");
        sb.AppendLine("      <FrameLength>0.1</FrameLength>");
        sb.AppendLine("      <LeftCoordinate>64</LeftCoordinate>");
        sb.AppendLine("      <RightCoordinate>80</RightCoordinate>");
        sb.AppendLine("      <TopCoordinate>32</TopCoordinate>");
        sb.AppendLine("      <BottomCoordinate>48</BottomCoordinate>");
        sb.AppendLine("      <RelativeY>-7</RelativeY>");
        sb.AppendLine("    </Frame>");
        sb.AppendLine("  </AnimationChain>");
        sb.AppendLine("</AnimationChainArraySave>");
        WriteRaw(path, sb.ToString());

        list.TryReloadFrom(path, content).ShouldBeTrue();

        list["Walk"].ShouldBeSameAs(walkBefore);
        walkBefore![0].RelativeY.ShouldBe(-7f);
    }
}
