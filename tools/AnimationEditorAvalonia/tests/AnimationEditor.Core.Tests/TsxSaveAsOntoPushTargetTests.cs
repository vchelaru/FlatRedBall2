using AnimationEditor.DocumentMatrix;
using Shouldly;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>#2059: a .tsx is a native project or an associated tileset, never both.</summary>
[Collection("SequentialSingletons")]
public class TsxSaveAsOntoPushTargetTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private string? _refusal;

    public void Dispose() => _dir.Dispose();

    private async Task<string> OpenTsxAndSaveAs(string target)
    {
        string tsx = Documents.Write(_dir.Path, DocType.Tsx, Documents.FrontName);
        await _ctx.AppCommands.OpenProjectWorkflowAsync(tsx);
        _ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
        _ctx.AppCommands.SaveAsRefused += message => _refusal = message;

        await _ctx.AppCommands.SaveCurrentAnimationChainListAsync();
        return tsx;
    }

    [Fact]
    public async Task SaveAs_TsxOntoAPushTarget_IsRefused()
    {
        string target = Documents.Write(_dir.Path, DocType.Tsx, Documents.BackgroundName);
        string targetText = File.ReadAllText(target);
        string achx = Documents.Write(_dir.Path, DocType.Achx, "source");
        _ctx.IoManager.AddAssociatedTiledTilesetPath(achx, target);

        string tsx = await OpenTsxAndSaveAs(target);

        new FilePath(_ctx.ProjectManager.FileName!).ShouldBe(new FilePath(tsx));
        File.ReadAllText(target).ShouldBe(targetText);
        _refusal!.ShouldContain("source.achx");
    }

    [Fact]
    public async Task SaveAs_TsxBesideAFolderThatCannotBeRead_IsSaved()
    {
        if (!OperatingSystem.IsWindows()) return;
        string locked = Directory.CreateDirectory(Path.Combine(_dir.Path, "locked")).FullName;
        string target = Path.Combine(_dir.Path, "copy.tsx");
        Icacls($"\"{locked}\" /deny *S-1-1-0:(RD)");
        try
        {
            await OpenTsxAndSaveAs(target);
        }
        finally
        {
            Icacls($"\"{locked}\" /remove:d *S-1-1-0");
        }

        File.Exists(target).ShouldBeTrue(_refusal);
    }

    private static void Icacls(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("icacls", arguments) { UseShellExecute = false, RedirectStandardOutput = true })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0);
    }
}
