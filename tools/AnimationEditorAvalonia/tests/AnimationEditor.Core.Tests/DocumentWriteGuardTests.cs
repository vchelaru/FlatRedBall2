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
        ["AppCommands.SaveCurrentAnimationChainListAsync"] = [DocAction.SaveAs, DocAction.SaveAsOntoAnotherFormat, DocAction.SaveAsOntoOpenTab],
        ["AppCommands.SaveDocument"] = [DocAction.SaveBackgroundDocument, DocAction.CutChain, DocAction.CutFrame, DocAction.CutShape],
        ["AppCommands.SaveTsxDocument"] = [DocAction.SaveBackgroundDocument, DocAction.CutChain, DocAction.CutFrame],
        ["AppCommands.SyncAssociatedTiledTilesets"] = [DocAction.PushToTileset],
        ["TiledTilesetSyncRunner.SyncAll"] = [DocAction.PushToTileset],
        ["AppCommands.ExportAsync"] = [DocAction.Export],
        ["AppCommands.CopyReferencedTextures"] = [DocAction.Export],
        ["ProjectManager.SaveAnimationChainList"] = [DocAction.Save, DocAction.SaveBackgroundDocument],
        ["ProjectManager.SaveTsxProject"] = [DocAction.Save, DocAction.SaveBackgroundDocument],
        ["IoManager.WriteRecoveryFile"] = [DocAction.WriteRecovery, DocAction.RecoverAtStartup],
        ["AchxToAchjConverter.Convert"] = [DocAction.ConvertToAchj, DocAction.ConvertOpenTabToAchj],
        ["TsxWriter.Write"] = [DocAction.Save, DocAction.PushToTileset],
        ["TsxWriter.TryWritePatchedCore"] = [DocAction.Save, DocAction.PushToTileset],
        ["PreviewControl.CommitChainDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitFrameDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitShapeDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitMultiShapeDrag"] = [DocAction.Save, DocAction.Autosave],
        ["PreviewControl.CommitShapeResize"] = [DocAction.Save, DocAction.Autosave],
        ["TreeMenuPlanBuilder.MatchFrameSize"] = [DocAction.Save, DocAction.Autosave],
        ["SequenceDocument.Restore"] = [DocAction.Autosave],
        ["SequenceDocument.SaveAs"] = [DocAction.SaveAs, DocAction.SaveAsOntoAnotherFormat],
        ["SequenceDocument.SaveToDisk"] = [DocAction.Save, DocAction.Autosave, DocAction.CutSequence],
        ["MainWindow.RemoveCutSourcesAndSaveTheirDocument"] = [DocAction.CutChain, DocAction.CutChainATilesetCannotStore, DocAction.CutFrame, DocAction.CutShape],
        ["MainWindow.OnNewClick"] = [DocAction.SaveAs],
        ["MainWindow.ConvertToAchjAsync"] = [DocAction.ConvertOpenTabToAchj],
        ["MainWindow.AchxTabPane.PrepareCloseAsync"] = [DocAction.CloseTabWithUnsavedEdits, DocAction.CloseWindowWithUnsavedEdits, DocAction.Detach],
        ["MainWindow.AchxTabPane.ConfirmFailedSaveAsync"] = [DocAction.CloseTabWithUnsavedEdits, DocAction.CloseWindowWithUnsavedEdits],
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
        ["TsxCompatibilityChecker.CheckOpenCompatibility"] = "a trial write to nowhere",
        ["DiskEditorFolder.OpenWriteAsync"] = "opens the stream NewAnimationFileWriter fills",
        ["ProjectTreeThumbnailService.TrySaveToDisk"] = "the thumbnail cache",
        ["MainWindow.SaveSettingsFile"] = "the application settings",
        ["CleanShutdownMarker.Write"] = "diagnostics",
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
        @"\bFile\.(Write\w+|Append\w+|Move|Copy|Replace|Create|OpenWrite)\(|\bAtomicFile\.Write\(|\bTsxWriter\.\w+\(|\.Save(Json|As)?\([^)\s]"
        + @"|(?<!void |Task |bool |string> )\b(SaveCurrentAnimationChainList(Async)?|SaveDocument|SaveTsxProject|SaveTsxDocument|SaveAnimationChainList|SaveToDisk"
        + @"|WriteRecoveryFile|SyncAssociatedTiledTilesets)\(|\bTiledTilesetSyncRunner\.\w+\(|\bAchxToAchj\w+\.Convert\(|\bDeleteToRecycleBin\(",
        RegexOptions.Compiled);

    // A member declaration: an access modifier or "static" at member indentation, then the name before its parameters.
    private static readonly Regex Member = new(
        @"^(?: {4}|\t){1,2}(?:\[[^\]]+\]\s*)?(?:public|private|internal|protected|static)\b[^=;(]*?\b(\w+)(?:<[^>(]*>)?\(", RegexOptions.Compiled);

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
            string name = Path.GetFileName(file).Replace(".axaml", "").Replace(".cs", ""), member = "";
            foreach (string line in File.ReadLines(file))
            {
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                if (Member.Match(line) is { Success: true } declared) member = declared.Groups[1].Value;
                if (Write.IsMatch(line)) methods.Add($"{name}.{member}");
            }
        }
        return methods;
    }

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
