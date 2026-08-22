using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #953: the PNG usage-overlay scan finds every frame across a project's .achx/.achj files
// whose texture resolves to a given target PNG, in texture-space pixel coordinates.
public class AchxPngUsageScannerTests
{
    private const string Achx = """
        <AnimationChainArraySave>
            <CoordinateType>UV</CoordinateType>
            <AnimationChain>
                <Name>Walk</Name>
                <Frame>
                    <TextureName>hero.png</TextureName>
                    <LeftCoordinate>0.25</LeftCoordinate>
                    <RightCoordinate>0.75</RightCoordinate>
                    <TopCoordinate>0</TopCoordinate>
                    <BottomCoordinate>0.5</BottomCoordinate>
                </Frame>
            </AnimationChain>
        </AnimationChainArraySave>
        """;

    private const string AchxPixelCoordinates = """
        <AnimationChainArraySave>
            <CoordinateType>Pixel</CoordinateType>
            <AnimationChain>
                <Name>Run</Name>
                <Frame>
                    <TextureName>hero.png</TextureName>
                    <LeftCoordinate>16</LeftCoordinate>
                    <RightCoordinate>48</RightCoordinate>
                    <TopCoordinate>0</TopCoordinate>
                    <BottomCoordinate>32</BottomCoordinate>
                </Frame>
            </AnimationChain>
        </AnimationChainArraySave>
        """;

    private const string AchxOtherTexture = """
        <AnimationChainArraySave>
            <CoordinateType>UV</CoordinateType>
            <AnimationChain>
                <Name>Idle</Name>
                <Frame>
                    <TextureName>villain.png</TextureName>
                    <LeftCoordinate>0</LeftCoordinate>
                    <RightCoordinate>1</RightCoordinate>
                    <TopCoordinate>0</TopCoordinate>
                    <BottomCoordinate>1</BottomCoordinate>
                </Frame>
            </AnimationChain>
        </AnimationChainArraySave>
        """;

    [Fact]
    public async Task FindMatchesAsync_UvCoordinateType_ConvertsFrameToTargetPixelSize()
    {
        var hero = new FakeEditorFile("hero.png");
        var folder = new FakeEditorFolder("Content");
        folder.Files.Add(hero);
        var entry = new AchxFileEntry(new FakeEditorFile("chain.achx", Achx), folder, "chain.achx");

        var matches = await AchxPngUsageScanner.FindMatchesAsync(
            entry, isTargetTexture: f => ReferenceEquals(f, hero),
            targetTextureWidth: 64, targetTextureHeight: 64);

        var match = Assert.Single(matches);
        Assert.Equal("Walk", match.Chain.Name);
        Assert.Equal(16f, match.Left, tolerance: 0.001f);
        Assert.Equal(48f, match.Right, tolerance: 0.001f);
        Assert.Equal(0f, match.Top, tolerance: 0.001f);
        Assert.Equal(32f, match.Bottom, tolerance: 0.001f);
    }

    [Fact]
    public async Task FindMatchesAsync_PixelCoordinateType_PassesFrameThroughUnconverted()
    {
        var hero = new FakeEditorFile("hero.png");
        var folder = new FakeEditorFolder("Content");
        folder.Files.Add(hero);
        var entry = new AchxFileEntry(new FakeEditorFile("chain.achx", AchxPixelCoordinates), folder, "chain.achx");

        var matches = await AchxPngUsageScanner.FindMatchesAsync(
            entry, isTargetTexture: f => ReferenceEquals(f, hero),
            targetTextureWidth: 64, targetTextureHeight: 64);

        var match = Assert.Single(matches);
        Assert.Equal("Run", match.Chain.Name);
        Assert.Equal(16f, match.Left, tolerance: 0.001f);
        Assert.Equal(48f, match.Right, tolerance: 0.001f);
    }

    [Fact]
    public async Task FindMatchesAsync_FrameReferencesDifferentTexture_IsExcluded()
    {
        var hero = new FakeEditorFile("hero.png");
        var folder = new FakeEditorFolder("Content");
        folder.Files.Add(hero);
        folder.Files.Add(new FakeEditorFile("villain.png"));
        var entry = new AchxFileEntry(new FakeEditorFile("chain.achx", AchxOtherTexture), folder, "chain.achx");

        var matches = await AchxPngUsageScanner.FindMatchesAsync(
            entry, isTargetTexture: f => ReferenceEquals(f, hero),
            targetTextureWidth: 64, targetTextureHeight: 64);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_TextureInNestedFolder_ResolvesRelativeToAchxOwnFolder()
    {
        // The achx lives in a "Sprites" subfolder and its texture reference is relative to THAT
        // folder, not some other root -- ParentFolder must be what ResolveRelativeFileAsync uses.
        var hero = new FakeEditorFile("hero.png");
        var spritesFolder = new FakeEditorFolder("Sprites");
        spritesFolder.Files.Add(hero);
        var entry = new AchxFileEntry(new FakeEditorFile("chain.achx", Achx), spritesFolder, "Sprites/chain.achx");

        var matches = await AchxPngUsageScanner.FindMatchesAsync(
            entry, isTargetTexture: f => ReferenceEquals(f, hero),
            targetTextureWidth: 64, targetTextureHeight: 64);

        Assert.Single(matches);
    }

    [Fact]
    public async Task FindMatchesAsync_MultipleAchxFiles_AggregatesMatchesFromEachEntry()
    {
        var hero = new FakeEditorFile("hero.png");
        var folder = new FakeEditorFolder("Content");
        folder.Files.Add(hero);
        var walkEntry = new AchxFileEntry(new FakeEditorFile("walk.achx", Achx), folder, "walk.achx");
        var runEntry = new AchxFileEntry(new FakeEditorFile("run.achx", AchxPixelCoordinates), folder, "run.achx");

        bool IsTarget(IEditorFile f) => ReferenceEquals(f, hero);

        var walkMatches = await AchxPngUsageScanner.FindMatchesAsync(walkEntry, IsTarget, 64, 64);
        var runMatches = await AchxPngUsageScanner.FindMatchesAsync(runEntry, IsTarget, 64, 64);
        var all = walkMatches.Concat(runMatches).ToList();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, m => m.Chain.Name == "Walk");
        Assert.Contains(all, m => m.Chain.Name == "Run");
    }

    [Fact]
    public async Task FindMatchesAsync_MalformedAchx_ReturnsEmptyInsteadOfThrowing()
    {
        var hero = new FakeEditorFile("hero.png");
        var folder = new FakeEditorFolder("Content");
        folder.Files.Add(hero);
        var entry = new AchxFileEntry(new FakeEditorFile("broken.achx", "not xml or json"), folder, "broken.achx");

        var matches = await AchxPngUsageScanner.FindMatchesAsync(
            entry, isTargetTexture: f => ReferenceEquals(f, hero), targetTextureWidth: 64, targetTextureHeight: 64);

        Assert.Empty(matches);
    }
}
