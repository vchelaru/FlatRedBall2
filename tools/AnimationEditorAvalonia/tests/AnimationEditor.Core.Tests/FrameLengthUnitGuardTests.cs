using System.Text.RegularExpressions;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// #1829 guard: a frame length is in the file's unit, and only code that goes through
/// <c>FrameLengthUnit</c> (or merely copies the value) may touch <c>FrameLength</c>. A new file that
/// reads or writes it fails here until it is routed through the unit and added to
/// <see cref="Reviewed"/>; a hard-coded seconds-sized literal fails wherever it appears.
/// </summary>
public class FrameLengthUnitGuardTests
{
    /// <summary>Files reviewed to handle the unit correctly (they use FrameLengthUnit, or only copy/compare the raw value).</summary>
    private static readonly string[] Reviewed =
    {
        "Core/CommandsAndState/AppCommands.cs",
        "Core/CommandsAndState/BulkFrameEditCommand.cs",
        "Core/DragDrop/TextureDropProcessor.cs",
        "Core/Rendering/BatchFrameBuilder.cs",
        "Core/IO/AnimationCloneHelper.cs",
        "Core/IO/ClipboardPayload.cs",
        "Core/Export/GodotSpriteFramesExporter.cs",
        "Core/Export/PixiJsSpriteSheetExporter.cs",
        "Core/Tiled/AchjSyncSnapshot.cs",
        "Core/Tiled/AchjToTiledAnimationMapper.cs",
        "Core/Tiled/MultiTileToTiledAnimationMapper.cs",
        "Core/Tiled/TiledAnimationToAchjMapper.cs",
        "Core/ViewModels/TimelineStripSignature.cs",
        "Core/CommandsAndState/PlaybackController.cs",
        "App/MainWindow.axaml.cs",
        "Core/Rendering/FrameTimeScaler.cs",
        "Core/ViewModels/TimelineBuilder.cs",
        "Core/ViewModels/TreeBuilder.cs",
        "Views/Dialogs/EditorDialogs.cs",
    };

    /// <summary>Files still treating the raw value as seconds. Empty: every file that touches FrameLength is reviewed.</summary>
    private static readonly string[] Pending = System.Array.Empty<string>();

    /// <summary>
    /// Files that pass a hard-coded <c>FrameLengthUnit.Seconds</c> instead of the unit of the file they play. Nothing else
    /// may name a unit constant: a file's unit comes from its list (<c>FrameLengthUnit.Of</c>). The sequence tab's sources
    /// each have their own unit. Empty since #1829 slice 5, and it stays empty.
    /// </summary>
    private static readonly string[] UnitPlaceholders = System.Array.Empty<string>();

    private static readonly Regex UnitConstant = new(@"FrameLengthUnit\.(Seconds|Milliseconds)\b", RegexOptions.Compiled);

    private static readonly Regex Touches = new(@"\bFrameLength\b", RegexOptions.Compiled);
    private static readonly Regex SecondsLiteral = new(@"FrameLength\s*(\?\?|=)\s*\d|FrameLength:\s*0\.|\?\?\s*0\.1f?\b", RegexOptions.Compiled);

    private static string SourceRoot()
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tools", "AnimationEditorAvalonia", "src");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not find tools/AnimationEditorAvalonia/src above " + AppContext.BaseDirectory);
    }

    private static string Key(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/').Replace("AnimationEditor.", "");

    private static Dictionary<string, string> CodeByFile()
    {
        string root = SourceRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToDictionary(p => Key(root, p), p => string.Join('\n',
                File.ReadAllLines(p).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))));
    }

    [Fact]
    public void EveryFileTouchingFrameLength_IsReviewedForTheUnit()
    {
        var known = Reviewed.Concat(Pending).ToHashSet();
        var unknown = CodeByFile().Where(f => Touches.IsMatch(f.Value)).Select(f => f.Key).Where(k => !known.Contains(k)).OrderBy(k => k).ToList();

        Assert.True(unknown.Count == 0,
            "These files read or write FrameLength but are not reviewed for the file's time unit. Route them through "
            + "FrameLengthUnit (AnimationEditor.Core.Utilities), then add them to Reviewed: " + string.Join(", ", unknown));
    }

    [Fact]
    public void ReviewedAndPendingLists_HaveNoStaleEntries()
    {
        var code = CodeByFile();
        var stale = Reviewed.Concat(Pending).Where(k => !code.TryGetValue(k, out var text) || !Touches.IsMatch(text)).ToList();

        Assert.True(stale.Count == 0, "No longer touch FrameLength (remove from the list): " + string.Join(", ", stale));
    }

    [Fact]
    public void NoReviewedFile_HardCodesASecondsSizedFrameLength()
    {
        var code = CodeByFile();
        var offenders = Reviewed.Where(k => code.TryGetValue(k, out var text) && SecondsLiteral.IsMatch(text)).ToList();

        Assert.True(offenders.Count == 0,
            "Hard-coded frame length literal (a Millisecond file needs 100, not 0.1). Use FrameLengthUnit.DefaultFrameLength: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void OnlyTheListedPlaceholders_NameAUnitConstant()
    {
        var code = CodeByFile();
        var offenders = code.Where(f => !f.Key.EndsWith("Utilities/FrameLengthUnit.cs") && UnitConstant.IsMatch(f.Value))
            .Select(f => f.Key).Where(k => !UnitPlaceholders.Contains(k)).OrderBy(k => k).ToList();
        Assert.True(offenders.Count == 0,
            "Hard-coded FrameLengthUnit.Seconds/Milliseconds: take the unit from the file being played (FrameLengthUnit.Of(list)): "
            + string.Join(", ", offenders));

        var stale = UnitPlaceholders.Where(k => !code.TryGetValue(k, out var text) || !UnitConstant.IsMatch(text)).ToList();
        Assert.True(stale.Count == 0, "No longer hard-code a unit (remove from UnitPlaceholders): " + string.Join(", ", stale));
    }
}
