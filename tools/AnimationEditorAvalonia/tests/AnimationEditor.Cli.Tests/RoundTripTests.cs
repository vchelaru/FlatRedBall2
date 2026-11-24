using System.Reflection;
using System.Text;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class RoundTripTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task LoadSave_WithoutEdits_IsByteIdentical(string file)
    {
        var before = _w.Bytes(file);

        var open = await EditSession.OpenAsync(_w.PathOf(file));
        open.Result.Success.ShouldBeTrue(string.Join("; ", open.Result.Errors));
        open.Session!.Save().Success.ShouldBeTrue();

        _w.Bytes(file).ShouldBe(before);
    }

    [Theory]
    [InlineData("hero.achx", "rename-chain Walk to=Stride", "Walk", "Stride")]
    [InlineData("hero.achj", "rename-chain Walk to=Stride", "Walk", "Stride")]
    [InlineData("hero.seqj", "rename-sequence Run to=Dash", "Run", "Dash")]
    public async Task Edit_LfFile_ChangesOnlyTheRenamedLine(string file, string op, string from, string to)
    {
        var path = _w.PathOf(file);
        if (file.EndsWith("seqj")) new SequenceListSave { Sequences = { new SequenceSave { Name = from } } }.Save(path);
        var before = File.ReadAllText(path).ReplaceLineEndings("\n") + "\n";
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(before));

        (await _w.RunAsync("edit", path, "--op", op)).Code.ShouldBe(0);

        Encoding.UTF8.GetString(_w.Bytes(file)).ShouldBe(before.Replace(from, to));
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task LoadSaveLoad_KeepsTheWholeModel(string file)
    {
        var first = (await EditSession.OpenAsync(_w.PathOf(file))).Session!;
        var firstJson = first.Document.ToJsonString();
        first.Save();

        var second = (await EditSession.OpenAsync(_w.PathOf(file))).Session!;

        second.Document.ToJsonString().ShouldBe(firstJson);
    }

    [Fact]
    public async Task SavingAnAchjKeepsItJson_AndAnAchxKeepsItXml()
    {
        (await EditSession.OpenAsync(_w.PathOf("hero.achj"))).Session!.Save();
        (await EditSession.OpenAsync(_w.PathOf("hero.achx"))).Session!.Save();

        File.ReadAllText(_w.PathOf("hero.achj")).TrimStart().ShouldStartWith("{");
        File.ReadAllText(_w.PathOf("hero.achx")).TrimStart().ShouldStartWith("<?xml");
    }

    [Fact]
    public async Task Edits_DoNotAutosave_UntilSaveIsCalled()
    {
        var before = _w.Bytes("hero.achx");
        var session = (await EditSession.OpenAsync(_w.PathOf("hero.achx"))).Session!;

        var r = RenameEdit.RenameChain(session.ProjectManager, session.Commands, session.Document.AnimationChains[0], "Run");

        r.Success.ShouldBeTrue();
        _w.Bytes("hero.achx").ShouldBe(before);
        session.Save().Success.ShouldBeTrue();
        _w.Bytes("hero.achx").ShouldNotBe(before);
    }

    [Fact]
    public void TheEditingLogicReferencesNoUiAssemblies()
    {
        var seen = new HashSet<string>();
        // The CLI itself now references Views for `render`; the editing logic behind every other command stays UI-free.
        var queue = new Queue<Assembly>([typeof(AnimationEditor.Editing.SequenceFileReader).Assembly]);
        while (queue.Count > 0)
        {
            var asm = queue.Dequeue();
            foreach (var name in asm.GetReferencedAssemblies())
            {
                if (!seen.Add(name.Name!)) continue;
                try { queue.Enqueue(Assembly.Load(name)); } catch (FileNotFoundException) { }
            }
        }

        seen.Where(n => n.StartsWith("Avalonia") || n.StartsWith("SkiaSharp")).ShouldBeEmpty();
    }
}
