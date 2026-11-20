using AnimationEditor.Core.IO;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class NewFileEditTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));

    public NewFileEditTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData("Enemy.achx")]
    [InlineData("Enemy.achj")]
    public void CreateAnimationFile_WritesExactlyWhatTheEditorWroteBefore_AndLoads(string name)
    {
        var path = Path.Combine(_root, name);

        var result = NewFileEdit.CreateAnimationFile(path);

        result.Success.ShouldBeTrue();
        result.ChangedItems.ShouldBe(new[] { path });
        // Pin: byte-identical to the writer the editor used directly before this op existed.
        using var expected = new MemoryStream();
        NewAnimationFileWriter.WriteEmpty(expected, name);
        File.ReadAllBytes(path).ShouldBe(expected.ToArray());
        var loaded = AnimationChainListSave.FromFile(path);
        loaded.AnimationChains.Count.ShouldBe(0);
        loaded.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);
    }

    [Fact]
    public void CreateAnimationFile_AlreadyExists_FailsAndLeavesItAlone()
    {
        var path = Path.Combine(_root, "Enemy.achj");
        File.WriteAllText(path, "original contents");

        var result = NewFileEdit.CreateAnimationFile(path);

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.AlreadyExists).ShouldBeTrue();
        result.Errors[0].ShouldBe("Could not create Enemy.achj: it already exists.");
        File.ReadAllText(path).ShouldBe("original contents");
    }

    [Fact]
    public void CreateAnimationFile_MissingFolder_FailsWithoutThrowing()
    {
        var path = Path.Combine(_root, "NoSuchFolder", "Enemy.achx");

        var result = NewFileEdit.CreateAnimationFile(path);

        result.Success.ShouldBeFalse();
        result.HasError(EditErrorCode.AlreadyExists).ShouldBeFalse();
        result.Errors[0].ShouldStartWith("Could not create Enemy.achx: ");
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void CreateSequenceFile_WritesOneEmptySequence_AndLoads()
    {
        var path = Path.Combine(_root, "Boss.seqj");

        var result = NewFileEdit.CreateSequenceFile(path);

        result.Success.ShouldBeTrue();
        result.ChangedItems.ShouldBe(new[] { path });
        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Sequence" });
        var expected = new SequenceListSave();
        expected.Sequences.Add(new SequenceSave { Name = "Sequence" });
        var reference = Path.Combine(_root, "reference.seqj");
        expected.Save(reference);
        File.ReadAllBytes(path).ShouldBe(File.ReadAllBytes(reference));
    }

    [Fact]
    public void CreateSequenceFile_AlreadyExists_FailsAndLeavesItAlone()
    {
        var path = Path.Combine(_root, "Boss.seqj");
        File.WriteAllText(path, "original contents");

        var result = NewFileEdit.CreateSequenceFile(path);

        result.HasError(EditErrorCode.AlreadyExists).ShouldBeTrue();
        result.Errors[0].ShouldBe("Could not create Boss.seqj: it already exists.");
        File.ReadAllText(path).ShouldBe("original contents");
    }

    [Fact]
    public void CreateSequenceFile_OverwriteTrue_ReplacesExisting()
    {
        var path = Path.Combine(_root, "Boss.seqj");
        File.WriteAllText(path, "original contents");

        var result = NewFileEdit.CreateSequenceFile(path, overwrite: true);

        result.Success.ShouldBeTrue();
        SequenceListSave.FromFile(path).Sequences.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateSequenceFile_MissingFolder_FailsWithoutThrowing()
    {
        var result = NewFileEdit.CreateSequenceFile(Path.Combine(_root, "NoSuchFolder", "Boss.seqj"));

        result.Success.ShouldBeFalse();
        result.Errors[0].ShouldStartWith("Could not create Boss.seqj: ");
    }
}
