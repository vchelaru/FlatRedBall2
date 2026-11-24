using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The editor's own save paths keep an LF file LF; SaveLineEndingTests (Common) has the full table.</summary>
public class EditorSaveLineEndingTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public void SaveCurrentAnimationChainList_LfFile_ChangesOnlyTheRenamedLine(string file)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var source = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        source.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        string path = Path.Combine(_dir.Path, file);
        string text = WriteAsLf(path, file.EndsWith("achj") ? source.SaveJson : source.Save);
        ctx.ProjectManager.LoadAnimationChain(new FilePath(path));

        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Name = "Stride";
        ctx.AppCommands.SaveCurrentAnimationChainList(path);

        ReadExact(path).ShouldBe(text.Replace("Walk", "Stride"));
    }

    [Fact]
    public void RenameSequence_LfFile_ChangesOnlyTheRenamedLine()
    {
        var source = new SequenceListSave { Sequences = { new SequenceSave { Name = "Run" } } };
        string path = Path.Combine(_dir.Path, "hero.seqj");
        string text = WriteAsLf(path, source.Save);

        SequenceDocument.Load(path).RenameSequence(new UndoManager(), 0, "Dash").ShouldBeNull();

        ReadExact(path).ShouldBe(text.Replace("Run", "Dash"));
    }

    [Fact]
    public void SaveAs_LfSequenceToANewPath_WritesTheDefaultLineEnding()
    {
        var source = new SequenceListSave { Sequences = { new SequenceSave { Name = "Run" } } };
        string path = Path.Combine(_dir.Path, "hero.seqj");
        WriteAsLf(path, source.Save);
        string copy = Path.Combine(_dir.Path, "copy.seqj");

        SequenceDocument.Load(path).SaveAs(copy).ShouldBeNull();

        ReadExact(copy).ShouldBe(source.ToJsonString().ReplaceLineEndings());
    }

    private static string WriteAsLf(string path, Action<string> save)
    {
        save(path);
        string text = File.ReadAllText(path).ReplaceLineEndings("\n") + "\n";
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
        return text;
    }

    private static string ReadExact(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));
}
