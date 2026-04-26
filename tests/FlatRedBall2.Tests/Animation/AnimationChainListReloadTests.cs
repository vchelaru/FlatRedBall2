using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Covers AnimationChainList.TryReloadFrom — the in-place reload path for .achx edits during
// hot-reload. Design intent: preserve chain-instance identity by name so any live
// Sprite.CurrentAnimation reference keeps playing from the updated frames without resetting.
public class AnimationChainListReloadTests
{
    // In-memory file system: path → XML bytes. Routed through ContentManagerService.StreamProvider
    // so the engine never touches the disk and matches the production WASM/desktop code path
    // (TitleContainer.OpenStream rejects absolute filesystem paths).
    private readonly Dictionary<string, byte[]> _virtualFiles = new(StringComparer.OrdinalIgnoreCase);

    private ContentManagerService MakeContent()
    {
        var svc = new ContentManagerService();
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

    private AnimationChainList LoadFresh(string path, ContentManagerService content)
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
