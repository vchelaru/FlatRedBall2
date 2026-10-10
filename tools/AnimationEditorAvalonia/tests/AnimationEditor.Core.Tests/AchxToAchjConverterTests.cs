using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Bulk .achx to .achj conversion (#1376): only the serialization changes, so chains, frame
/// values, and the coordinate type survive, and a conversion never overwrites an existing file.
/// </summary>
public class AchxToAchjConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));

    public AchxToAchjConverterTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void WriteAchx(string path, string chainName, TextureCoordinateType coordinateType)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var acls = new AnimationChainListSave { CoordinateType = coordinateType };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "hero.png",
            FrameLength = 0.25f,
            LeftCoordinate = 16,
            RightCoordinate = 32,
            TopCoordinate = 0,
            BottomCoordinate = 16,
        });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
    }

    [Fact]
    public void Convert_AchxFile_WritesAchjWithSameContentAndCoordinateType()
    {
        var source = Path.Combine(_root, "hero.achx");
        WriteAchx(source, "Walk", TextureCoordinateType.Pixel);

        var result = AchxToAchjConverter.Convert(source);

        var expectedTarget = Path.Combine(_root, "hero.achj");
        result.Status.ShouldBe(AchxConversionStatus.Converted);
        result.TargetPath.ShouldBe(expectedTarget);
        var converted = AnimationChainListSave.FromJsonFile(expectedTarget);
        converted.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);
        converted.AnimationChains.Single().Name.ShouldBe("Walk");
        var frame = converted.AnimationChains.Single().Frames.Single();
        frame.TextureName.ShouldBe("hero.png");
        frame.FrameLength.ShouldBe(0.25f);
        frame.RightCoordinate.ShouldBe(32f);
        File.Exists(source).ShouldBeTrue();
    }

    [Fact]
    public void Convert_AchjAlreadyExists_SkipsAndLeavesItUntouched()
    {
        var source = Path.Combine(_root, "hero.achx");
        var existing = Path.Combine(_root, "hero.achj");
        WriteAchx(source, "Walk", TextureCoordinateType.Pixel);
        File.WriteAllText(existing, "keep me");

        var result = AchxToAchjConverter.Convert(source);

        result.Status.ShouldBe(AchxConversionStatus.SkippedTargetExists);
        File.ReadAllText(existing).ShouldBe("keep me");
    }

    [Fact]
    public void Convert_UnparseableAchx_ReportsFailureAndWritesNothing()
    {
        var source = Path.Combine(_root, "broken.achx");
        File.WriteAllText(source, "<<<<<<< HEAD\nnot xml\n=======\n>>>>>>> branch");

        var result = AchxToAchjConverter.Convert(source);

        result.Status.ShouldBe(AchxConversionStatus.Failed);
        result.Error.ShouldNotBeNullOrEmpty();
        File.Exists(Path.Combine(_root, "broken.achj")).ShouldBeFalse();
    }

    [Fact]
    public void ConvertFolder_NestedFolders_ConvertsEveryAchxExceptUnderBinAndObj()
    {
        WriteAchx(Path.Combine(_root, "a.achx"), "A", TextureCoordinateType.UV);
        WriteAchx(Path.Combine(_root, "Sub", "b.achx"), "B", TextureCoordinateType.UV);
        WriteAchx(Path.Combine(_root, "bin", "Debug", "c.achx"), "C", TextureCoordinateType.UV);

        var results = AchxToAchjConverter.ConvertFolder(_root);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => r.Status == AchxConversionStatus.Converted);
        File.Exists(Path.Combine(_root, "a.achj")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "Sub", "b.achj")).ShouldBeTrue();
        File.Exists(Path.Combine(_root, "bin", "Debug", "c.achj")).ShouldBeFalse();
    }
}
