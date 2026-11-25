using AnimationEditor.Core.IO;
using AnimationEditor.DocumentMatrix;
using Shouldly;
using System.Text.RegularExpressions;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #2006 guard: a method that writes a file, or asks for a document to be written, is in one of the lists below, so a new
/// one fails until it has rows in the matrix (<see cref="DocAction"/>) or a reason not to.
/// </summary>
public class DocumentWriteGuardTests
{
    /// <summary>"File.Method" of each method that writes a document, and the actions whose rows run it.</summary>
    private static readonly Dictionary<string, DocAction[]> WritesDocuments = new()
    {
        ["AppCommands.OnAnimationChainsChanged"] = [DocAction.Autosave],
        ["AppCommands.SaveCurrentAnimationChainList"] = [DocAction.Save, DocAction.Autosave, DocAction.WriteRecovery],
        ["AppCommands.SaveCurrentAnimationChainListAsync"] = [DocAction.SaveAs, DocAction.FailedSaveAs, DocAction.SaveAsOntoAnotherFormat, DocAction.SaveAsOntoOpenTab],
        ["AppCommands.SaveOpenDocument"] = [DocAction.Save, DocAction.Autosave, DocAction.WriteRecovery, DocAction.SaveAs, DocAction.FailedSaveAs,
            DocAction.SaveAsOntoAnotherFormat, DocAction.SaveAsOntoOpenTab],
        ["AppCommands.SaveDocument"] = [DocAction.SaveBackgroundDocument, DocAction.CutChain, DocAction.CutFrame, DocAction.CutShape],
        ["AppCommands.WriteDocument"] = [DocAction.Save, DocAction.SaveAs, DocAction.SaveBackgroundDocument, DocAction.PushFromBackground,
            DocAction.CutChainOutOfPushSource, DocAction.ConvertOpenPushSource],
        ["AppCommands.SyncAssociatedTiledTilesets"] = [DocAction.PushToTileset, DocAction.PushFromBackground],
        ["TiledTilesetSyncRunner.SyncAll"] = [DocAction.PushToTileset, DocAction.PushFromBackground],
        ["AppCommands.ExportAsync"] = [DocAction.Export],
        ["ProjectManager.SaveTsx"] = [DocAction.Save, DocAction.SaveBackgroundDocument, DocAction.CutChain, DocAction.CutFrame],
        ["ProjectManager.SaveAnimationChainList"] = [DocAction.Save, DocAction.SaveBackgroundDocument],
        ["IoManager.WriteRecoveryFile"] = [DocAction.WriteRecovery, DocAction.RecoverAtStartup],
        ["AchxToAchjConverter.Convert"] = [DocAction.ConvertToAchj, DocAction.ConvertOpenTabToAchj],
        ["TsxFile.Save"] = [DocAction.Save, DocAction.SaveBackgroundDocument, DocAction.SaveAsToAnotherFolder],
        ["TsxWriter.Write"] = [DocAction.Save, DocAction.PushToTileset],
        ["PreviewControl.CommitChainDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitFrameDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitShapeDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitMultiShapeDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitShapeResize"] = [DocAction.Save, DocAction.Autosave],
        ["TreeMenuPlanBuilder.MatchFrameSize"] = [DocAction.Save, DocAction.Autosave],
        ["SequenceDocument.Restore"] = [DocAction.Autosave],
        ["SequenceDocument.SaveAs"] = [DocAction.SaveAs, DocAction.FailedSaveAs, DocAction.SaveAsOntoAnotherFormat],
        ["SequenceDocument.SaveToDisk"] = [DocAction.Save, DocAction.Autosave, DocAction.CutSequence],
        ["MainWindow.RemoveCutSourcesAndSaveTheirDocument"] = [DocAction.CutChain, DocAction.CutChainATilesetCannotStore, DocAction.CutFrame, DocAction.CutShape],
        ["MainWindow.OnNewClick"] = [DocAction.SaveAs],
        ["MainWindow.ConvertToAchjAsync"] = [DocAction.ConvertOpenTabToAchj],
        ["MainWindow.AchxTabPane.PrepareCloseAsync"] = [DocAction.CloseTabWithUnsavedEdits, DocAction.CloseWindowWithUnsavedEdits, DocAction.Detach],
        ["MainWindow.AchxTabPane.ConfirmFailedSaveAsync"] = [DocAction.CloseTabWithUnsavedEdits, DocAction.CloseWindowWithUnsavedEdits],
        ["MainWindow.AchxTabPane.SaveFailedEdits"] = [DocAction.ConvertOpenTabToAchj],
        ["MainWindow.AchxTabPane.SaveAsync"] = [DocAction.Save],
        ["MainWindow.AchxTabPane.SaveAsAsync"] = [DocAction.SaveAs, DocAction.SaveAsOntoOpenTab],
        ["MainWindow.SequenceTabPane.SaveAsync"] = [DocAction.Save],
        ["MainWindow.SequenceTabPane.SaveAsAsync"] = [DocAction.SaveAs, DocAction.SaveAsOntoOpenTab],
        ["MainWindow.SequenceTabPane.ConfirmCloseAsync"] = [DocAction.CloseTabWithUnsavedEdits, DocAction.CloseWindowWithUnsavedEdits],
    };

    /// <summary>"File.Method" of each other method that writes a file, and why it has no row.</summary>
    private static readonly Dictionary<string, string> WritesSomethingElse = new()
    {
        ["MainWindow.DeleteProjectFileAsync"] = "deletes a file the user confirmed",
        ["MainWindow.DuplicateProjectFileAsync"] = "copies a file's bytes to a new name",
        ["MainWindow.ExportAnimatedImageAsync"] = "a GIF or WebP, read from the document",
        ["NewAnimationFileWriter.WriteEmpty"] = "a new, empty file",
        ["RecycleBin.MoveToLinuxTrash"] = "the recycle bin itself",
        ["TextureCopyDecider.Apply"] = "a texture",
        ["IoManager.AddAssociatedTiledTilesetPath"] = "the .tiledsync beside a document",
        ["XmlFile.Serialize"] = "the .aeproperties beside a document",
        ["DiskEditorFolder.OpenWriteAsync"] = "opens the stream NewAnimationFileWriter fills",
        ["ProjectTreeThumbnailService.TrySaveToDisk"] = "the thumbnail cache",
        ["MainWindow.SaveSettingsFile"] = "the application settings",
        ["CleanShutdownMarker.Write"] = "diagnostics",
        ["FileLogger.Append"] = "diagnostics",
        ["SingleInstanceServer.SendToRunningInstanceAsync"] = "a pipe to the running editor",
        ["PreviewControl.BuildPolygonPath"] = "draws a path (SKPath.MoveTo)",
        ["CrashLogging.IsDirectoryWritable"] = "diagnostics",
        ["LogRotation.RotateIfLarge"] = "diagnostics",
        ["MainWindow.LogSelectionPerf"] = "diagnostics",
        ["MemoryProbeRunner.WriteReport"] = "diagnostics",
        ["PlatformWheelInput.Write"] = "diagnostics",
        ["PreviewControl.LogSelectionPerf"] = "diagnostics",
        ["TimelineDiagnostics.OnMark"] = "diagnostics",
        ["WindowDiagnosticsHub.RecordStall"] = "diagnostics",
    };

    /// <summary>Projects without tabs: the command-line tool edits the one file it is given.</summary>
    private static readonly string[] OutsideTheEditor = { "AnimationEditor.Cli", "AnimationEditor.Editing" };

    private static readonly Regex Write = new(
        @"\bFile\.(Write\w+|Append\w+|Move|Copy|Replace|Create\w*|Open|OpenWrite)\(|\bnew (StreamWriter|FileStream)\(|\.(MoveTo|OpenWrite|CreateText|AppendText)\("
        + @"|\bnew FileInfo\([^)]*\)\.\w+To\(|\bAtomicFile\.Write\(|\bTsxWriter\.\w+\(|\.Save(Json|As)?\([^)\s]"
        + @"|(?<!void |Task |bool |bool> |string> )\b(SaveCurrentAnimationChainList(Async)?|SaveOpenDocument|WriteDocument|SaveDocument|SaveToDisk"
        + @"|WriteRecoveryFile|SyncAssociatedTiledTilesets)\(|\bTiledTilesetSyncRunner\.\w+\(|\bAchxToAchj\w+\.Convert\(|\bDeleteToRecycleBin\(",
        RegexOptions.Compiled);

    // A member declaration: at member indentation, a modifier or a type, then the name before its parameters.
    private static readonly Regex Member = new(
        @"^(?: {4}){1,2}(?:\[[^\]]+\]\s*)?(?:(?:public|private|internal|protected|static)\b[^=;(]*?\b(\w+)"
        + @"|(?!(?:await|return|throw|new|else|using|var|yield|case|lock|foreach|if|while|switch|for|catch|when|is|and|or|not|in|out|ref)\b)"
        + @"(?:[\w<>\[\]?,.]+\s+)+(\w+))(?:<[^>(]*>)?\((?:.*=>|[^;]*$)", RegexOptions.Compiled);

    private static string SourceRoot()
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tools", "AnimationEditorAvalonia", "src");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not find tools/AnimationEditorAvalonia/src above " + AppContext.BaseDirectory);
    }

    /// <summary>"File.Method" of every method in the editor's source that contains a write.</summary>
    private static HashSet<string> WritingMethods()
    {
        string root = SourceRoot();
        var methods = new HashSet<string>();
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/") || OutsideTheEditor.Contains(relative.Split('/')[0])) continue;
            string name = Path.GetFileName(file).Replace(".axaml", "").Replace(".cs", "");
            methods.UnionWith(WritingMethodsIn(File.ReadLines(file)).Select(method => $"{name}.{method}"));
        }
        return methods;
    }

    private static IEnumerable<string> WritingMethodsIn(IEnumerable<string> code)
    {
        string member = "";
        foreach (string line in code)
        {
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            if (Member.Match(line) is { Success: true } declared) member = declared.Groups[1].Value + declared.Groups[2].Value;
            if (Write.IsMatch(line)) yield return member;
        }
    }

    [Theory]
    [InlineData("    public void Store()", "        using var writer = new StreamWriter(path);")]
    [InlineData("    public void Store()", "        using var stream = new FileStream(path, FileMode.Create);")]
    [InlineData("    public void Store()", "        new FileInfo(from).MoveTo(to);")]
    [InlineData("    void Store(string path)", "        File.WriteAllText(path, text);")]
    [InlineData("    async Task<int> Store(", "        File.Copy(from, to);")]
    [InlineData("    void Store() => File.Delete(path);", "    void Other() => File.Move(from, to);")]
    public void AWritingMethod_IsFound(string declaration, string write) =>
        WritingMethodsIn(["    private int Before() => 1;", declaration, write]).ShouldBe([write.Contains("Other") ? "Other" : "Store"]);

    [Fact]
    public void EveryWritingMethod_HasRowsInTheMatrixOrAReason()
    {
        var known = WritesDocuments.Keys.Concat(WritesSomethingElse.Keys).ToHashSet();
        var found = WritingMethods();

        found.Except(known).OrderBy(name => name).ShouldBeEmpty(
            "These methods write a file. Give a document write rows in the matrix (DocAction) and list it in WritesDocuments, or say in WritesSomethingElse why it has none.");
        known.Except(found).OrderBy(name => name).ShouldBeEmpty("These methods write nothing any more: remove them from the list.");
    }

    [Fact]
    public void EveryFormatTheEditorOpens_IsAType() =>
        OpenFileFilters.AnimationFiles[0].Patterns.ShouldBe(
            Enum.GetValues<DocType>().Where(type => type != DocType.Untitled).Select(type => "*" + type.Extension()), ignoreOrder: true);
}
