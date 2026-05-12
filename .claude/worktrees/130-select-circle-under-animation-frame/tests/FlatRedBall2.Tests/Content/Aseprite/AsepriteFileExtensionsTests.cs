using System;
using System.IO;
using System.Linq;
using System.Text;
using AsepriteDotNet.Aseprite;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Content.Aseprite;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content.Aseprite;

public class AsepriteFileExtensionsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"frb2_ase_test_{Guid.NewGuid()}");

    public AsepriteFileExtensionsTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteTempAse(byte[] bytes, string name = "test.ase")
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private AsepriteFile LoadTestFile(string tagName = "Idle", int frames = 2, int durationMs = 100)
    {
        byte[] bytes = AsepriteTestFixture.Build(width: 4, height: 4, frameCount: frames,
            tagName: tagName, frameDurationMs: durationMs);
        string path = WriteTempAse(bytes);
        return AsepriteFileLoader.Load(path);
    }

    // ── Loader ──

    [Fact]
    public void Load_ValidAseFile_ReturnsNonNull()
    {
        byte[] bytes = AsepriteTestFixture.Build(4, 4, 2, "Idle", 100);
        string path = WriteTempAse(bytes);

        var file = AsepriteFileLoader.Load(path);

        file.ShouldNotBeNull();
    }

    // ── ToAnimationChainListSave ──

    [Fact]
    public void ToAnimationChainListSave_SingleTag_CreatesOneChainWithTagName()
    {
        string tagName = "Walk";
        var file = LoadTestFile(tagName: tagName);
        string textureName = "spritesheet.png";

        AnimationChainListSave save = file.ToAnimationChainListSave(textureName);

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe(tagName);
    }

    [Fact]
    public void ToAnimationChainListSave_TwoFrameTag_CreatesTwoFrames()
    {
        int frameCount = 2;
        var file = LoadTestFile(frames: frameCount);
        string textureName = "spritesheet.png";

        AnimationChainListSave save = file.ToAnimationChainListSave(textureName);

        save.AnimationChains[0].Frames.Count.ShouldBe(frameCount);
    }

    [Fact]
    public void ToAnimationChainListSave_Duration100ms_ConvertedToPointOneSeconds()
    {
        int durationMs = 100;
        float expectedSeconds = 0.1f;
        var file = LoadTestFile(durationMs: durationMs);
        string textureName = "spritesheet.png";

        AnimationChainListSave save = file.ToAnimationChainListSave(textureName);

        foreach (var frame in save.AnimationChains[0].Frames)
            frame.FrameLength.ShouldBe(expectedSeconds, tolerance: 0.001f);
    }

    [Fact]
    public void ToAnimationChainListSave_AllFrames_ReferenceProvidedTextureName()
    {
        string textureName = "Player.png";
        var file = LoadTestFile();

        AnimationChainListSave save = file.ToAnimationChainListSave(textureName);

        foreach (var frame in save.AnimationChains[0].Frames)
            frame.TextureName.ShouldBe(textureName);
    }

    [Fact]
    public void ToAnimationChainListSave_CoordinateType_IsPixel()
    {
        var file = LoadTestFile();

        AnimationChainListSave save = file.ToAnimationChainListSave("tex.png");

        save.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);
    }

    [Fact]
    public void ToAnimationChainListSave_UntaggedFile_CreatesSingleDefaultChainWithAllFrames()
    {
        int frameCount = 3;
        byte[] bytes = AsepriteTestFixture.BuildUntagged(width: 4, height: 4,
            frameCount: frameCount, frameDurationMs: 100);
        string path = WriteTempAse(bytes, "untagged.ase");
        var file = AsepriteFileLoader.Load(path);

        AnimationChainListSave save = file.ToAnimationChainListSave("tex.png");

        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe(FlatRedBall2.Content.Aseprite.AsepriteFileExtensions.UntaggedChainName);
        save.AnimationChains[0].Frames.Count.ShouldBe(frameCount);
    }

    [Fact]
    public void ToAnimationChainListSave_FrameRegions_HaveNonZeroDimensions()
    {
        var file = LoadTestFile();

        AnimationChainListSave save = file.ToAnimationChainListSave("tex.png");

        foreach (var frame in save.AnimationChains[0].Frames)
        {
            float width = frame.RightCoordinate - frame.LeftCoordinate;
            float height = frame.BottomCoordinate - frame.TopCoordinate;
            width.ShouldBeGreaterThan(0f);
            height.ShouldBeGreaterThan(0f);
        }
    }
}
