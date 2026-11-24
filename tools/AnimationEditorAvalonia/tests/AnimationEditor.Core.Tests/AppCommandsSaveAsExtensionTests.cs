using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Save As onto a name whose extension belongs to a type the document cannot be saved as is refused, with nothing
/// written and the document keeping its file. A name of no known type saves in the document's own format.
/// </summary>
[Collection("SequentialSingletons")]
public class AppCommandsSaveAsExtensionTests : IDisposable
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
        </tileset>
        """;

    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = TestHelpers.SetupFreshAcls();

    public void Dispose() => _dir.Dispose();

    private string At(string name) => Path.Combine(_dir.Path, name);

    /// <summary>Opens a document of the given type, or an Untitled one for null.</summary>
    private void Open(string? type)
    {
        if (type == "tsx")
        {
            File.WriteAllText(At("open.tsx"), TsxXml);
            _ctx.ProjectManager.LoadTsxProject(new FilePath(At("open.tsx")));
            return;
        }
        _ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        _ctx.ProjectManager.FileName = type == null ? null : At("open." + type);
    }

    private async Task<string?> SaveAsAsync(string path)
    {
        string? refusal = null;
        _ctx.AppCommands.SaveAsRefused += message => refusal = message;
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(path);
        await _ctx.AppCommands.SaveCurrentAnimationChainListAsync();
        return refusal;
    }

    [Theory]
    [InlineData(null, "heroes.tsx")]
    [InlineData(null, "heroes.seqj")]
    [InlineData(null, "heroes.png")]
    [InlineData("achx", "heroes.tsx")]
    [InlineData("achx", "heroes.TSX")]
    [InlineData("achx", "heroes.seqj")]
    [InlineData("achx", "heroes.png")]
    [InlineData("achj", "heroes.tsx")]
    [InlineData("achj", "heroes.seqj")]
    [InlineData("achj", "heroes.png")]
    [InlineData("tsx", "heroes.achx")]
    [InlineData("tsx", "heroes.AchX")]
    [InlineData("tsx", "heroes.achj")]
    [InlineData("tsx", "heroes.seqj")]
    [InlineData("tsx", "heroes.png")]
    public async Task SaveCurrentAnimationChainListAsync_AnotherTypesExtension_IsRefusedAndWritesNothing(string? type, string target)
    {
        Open(type);
        string? fileName = _ctx.ProjectManager.FileName;
        File.WriteAllText(At(target), "another file");
        bool completed = false;
        _ctx.AppCommands.SaveAsCompleted += _ => completed = true;

        string? refusal = await SaveAsAsync(At(target));

        string kind = type == "tsx" ? "a .tsx" : "an .achj or .achx";
        refusal.ShouldBe($"{target} is not {kind} file. Give it {kind} extension.");
        File.ReadAllText(At(target)).ShouldBe("another file");
        _ctx.ProjectManager.FileName.ShouldBe(fileName);
        completed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "heroes.achx", "<")]
    [InlineData(null, "heroes.achj", "{")]
    [InlineData("achx", "heroes.achx", "<")]
    [InlineData("achx", "heroes.achj", "{")]
    [InlineData("achx", "heroes.ACHJ", "{")]
    [InlineData("achx", "heroes", "<")]
    [InlineData("achx", "heroes.json", "<")]
    [InlineData("achj", "heroes.achx", "<")]
    [InlineData("achj", "heroes.achj", "{")]
    [InlineData("tsx", "heroes.tsx", "<")]
    [InlineData("tsx", "heroes.TSX", "<")]
    [InlineData("tsx", "heroes", "<")]
    public async Task SaveCurrentAnimationChainListAsync_OwnOrUnknownExtension_Saves(string? type, string target, string start)
    {
        Open(type);

        string? refusal = await SaveAsAsync(At(target));

        refusal.ShouldBeNull();
        File.ReadAllText(At(target)).ShouldStartWith(start);
        _ctx.ProjectManager.FileName.ShouldBe(At(target));
    }
}
