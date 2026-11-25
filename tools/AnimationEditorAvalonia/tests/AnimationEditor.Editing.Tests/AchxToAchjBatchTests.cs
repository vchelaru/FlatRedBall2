using AnimationEditor.Core.IO;
using AnimationEditor.Editing;
using AnimationEditor.Tests.Shared;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class AchxToAchjBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));

    public AchxToAchjBatchTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static void WriteAchx(string path, string chainName = "Walk")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "hero.png", FrameLength = 0.25f,
            LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 0, BottomCoordinate = 16,
        });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
    }

    [Fact]
    public void Convert_MixedBatch_ReportsConvertedSkippedAndFailedPerFile()
    {
        var ok = Path.Combine(_root, "ok.achx");
        var skip = Path.Combine(_root, "skip.achx");
        var bad = Path.Combine(_root, "bad.achx");
        WriteAchx(ok);
        WriteAchx(skip);
        File.WriteAllText(Path.Combine(_root, "skip.achj"), "keep me");
        File.WriteAllText(bad, "<<<<<<< HEAD\nnot xml\n=======\n>>>>>>> branch");

        var batch = AchxToAchjBatch.Convert(new[] { ok, skip, bad });

        batch.Converted.Select(f => f.SourcePath).ShouldBe(new[] { ok });
        batch.Skipped.Select(f => f.SourcePath).ShouldBe(new[] { skip });
        batch.Failed.Select(f => f.SourcePath).ShouldBe(new[] { bad });
        batch.Result.ChangedItems.ShouldBe(new[] { Path.Combine(_root, "ok.achj") });
        batch.Result.Warnings.Count.ShouldBe(1);
        batch.Result.Warnings[0].ShouldContain("skip.achx");
        batch.Result.Errors.Count.ShouldBe(1);
        batch.Result.Errors[0].ShouldContain("bad.achx");
        batch.Result.Success.ShouldBeFalse();
    }

    [Fact]
    public void Convert_FailedAndSkipped_WriteNothingAndLeaveExistingAchjUntouched()
    {
        var skip = Path.Combine(_root, "skip.achx");
        var bad = Path.Combine(_root, "bad.achx");
        WriteAchx(skip);
        File.WriteAllText(Path.Combine(_root, "skip.achj"), "keep me");
        File.WriteAllText(bad, "not xml at all");

        AchxToAchjBatch.Convert(new[] { skip, bad });

        File.ReadAllText(Path.Combine(_root, "skip.achj")).ShouldBe("keep me");
        File.Exists(Path.Combine(_root, "bad.achj")).ShouldBeFalse();
        File.Exists(skip).ShouldBeTrue();
        File.Exists(bad).ShouldBeTrue();
    }

    [Fact]
    public void Convert_AllConverted_IsSuccessWithNoWarnings()
    {
        var a = Path.Combine(_root, "a.achx");
        WriteAchx(a);

        var batch = AchxToAchjBatch.Convert(new[] { a });

        batch.Result.Success.ShouldBeTrue();
        batch.Result.Errors.ShouldBeEmpty();
        batch.Result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Convert_EmptyBatch_IsSuccessWithNothingChanged()
    {
        var batch = AchxToAchjBatch.Convert(Array.Empty<string>());

        batch.Files.ShouldBeEmpty();
        batch.Result.Success.ShouldBeTrue();
        batch.Result.ChangedItems.ShouldBeEmpty();
    }

    [Fact]
    public void Convert_RoundTrip_AchjBytesMatchDirectSerializationAndReloadsToSameAchx()
    {
        var source = Path.Combine(_root, "hero.achx");
        WriteAchx(source);

        AchxToAchjBatch.Convert(new[] { source });

        var expectedPath = Path.Combine(_root, "expected.achj");
        AnimationChainListSave.FromFile(source).SaveJson(expectedPath);
        File.ReadAllBytes(Path.Combine(_root, "hero.achj")).ShouldBe(File.ReadAllBytes(expectedPath));

        var viaAchj = Path.Combine(_root, "viaachj.achx");
        AnimationChainListSave.FromJsonFile(Path.Combine(_root, "hero.achj")).Save(viaAchj);
        var direct = Path.Combine(_root, "direct.achx");
        AnimationChainListSave.FromFile(source).Save(direct);
        File.ReadAllBytes(viaAchj).ShouldBe(File.ReadAllBytes(direct));
    }

    [Fact]
    public void FindSources_WalksSubfoldersSkipsBinObjAndIsSorted()
    {
        WriteAchx(Path.Combine(_root, "b.achx"));
        WriteAchx(Path.Combine(_root, "sub", "a.achx"));
        WriteAchx(Path.Combine(_root, "bin", "Debug", "copy.achx"));
        WriteAchx(Path.Combine(_root, "obj", "copy.achx"));
        File.WriteAllText(Path.Combine(_root, "c.achj"), "{}");

        var found = AchxToAchjBatch.FindSources(_root);

        found.ShouldBe(new[] { Path.Combine(_root, "b.achx"), Path.Combine(_root, "sub", "a.achx") });
    }

    [Fact]
    public void FindSources_SubfolderWithoutReadAccess_FindsTheRest()
    {
        WriteAchx(Path.Combine(_root, "hero.achx"));
        using var denied = new UnlistableFolder(Path.Combine(_root, "denied"));

        AchxToAchjBatch.FindSources(_root).ShouldBe(new[] { Path.Combine(_root, "hero.achx") });
    }
}
