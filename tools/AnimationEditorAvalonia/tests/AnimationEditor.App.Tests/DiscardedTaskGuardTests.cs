using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #2057, #2114 guard: a command that starts a task and does not await it loses what the task throws, and an async
/// event handler throws it at the app, so both go through <c>MainWindow.RunGuardedAsync</c>, which says it in the
/// problem banner.
/// </summary>
public class DiscardedTaskGuardTests
{
    private static readonly Regex Discard = new(@"(?<![\w])_ = (?<call>[^;]*?Async)\(");

    /// <summary>A lambda handler or dispatched lambda, up to its first await.</summary>
    private static readonly Regex AsyncHandler = new(@"(\+= |InvokeAsync\(|Post\()async\b.*?\bawait (?<call>[\w.]+)\(", RegexOptions.Singleline);

    /// <summary>First awaits of lambdas that need no guard, and why.</summary>
    private static readonly Dictionary<string, string> HandlerNeedsNoGuard = new()
    {
        ["PickCustomAsync"] = "a color dialog, which reads and writes no file",
        ["InvalidateProjectPanelThumbnailIfTrackedAsync"] = "a thumbnail, not a command",
        ["CloseTabCoreAsync"] = "the conversion waits for it, so it fails with it",
    };

    private static readonly Regex AsyncVoid = new(@"\basync void (?<call>\w+)\(");

    /// <summary>Async void methods that need no guard, and why.</summary>
    private static readonly Dictionary<string, string> AsyncVoidNeedsNoGuard = new()
    {
        ["LoadBlameForPng"] = "says what failed itself",
        ["UpdateDiffOverlay"] = "catches what it computes",
        ["OnPngUsageOverlayToggled"] = "says what failed itself",
        ["OnTreeDrop"] = "HandlePngDropAsync guards itself",
        ["OnTreePointerMoved"] = "starts a drag",
        ["OnTreeFrameDragPointerMoved"] = "starts a drag",
        ["OnTreeShapeDragPointerMoved"] = "starts a drag",
        ["OnTreeChainDragPointerMoved"] = "starts a drag",
    };

    /// <summary>Discarded calls that need no guard, and why.</summary>
    private static readonly Dictionary<string, string> NeedNoGuard = new()
    {
        ["HandleCopyAsync"] = "guards itself",
        ["HandleCutAsync"] = "guards itself",
        ["HandlePasteAsync"] = "guards itself",
        ["RunCheckAsync"] = "the updater returns a failure instead of throwing, and About shows it",
        ["InvalidateProjectPanelThumbnailIfTrackedAsync"] = "a thumbnail, not a command",
    };

    private static string AppSource()
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tools", "AnimationEditorAvalonia", "src", "AnimationEditor.App");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not find tools/AnimationEditorAvalonia/src/AnimationEditor.App above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> AppFiles() =>
        Directory.EnumerateFiles(AppSource(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    [Fact]
    public void EveryAsyncEventHandler_IsGuarded()
    {
        var unguarded = AppFiles()
            .SelectMany(path => new[] { (Regex: AsyncHandler, Except: HandlerNeedsNoGuard), (Regex: AsyncVoid, Except: AsyncVoidNeedsNoGuard) }
                .SelectMany(rule => rule.Regex.Matches(File.ReadAllText(path))
                    .Select(match => match.Groups["call"].Value.Split('.').Last())
                    .Where(call => call != "RunGuardedAsync" && !rule.Except.ContainsKey(call))
                    .Select(call => $"{Path.GetFileName(path)}: {call}")))
            .ToList();

        unguarded.ShouldBeEmpty("start it with += (_, _) => _ = RunGuardedAsync(() => ..., \"Name\")");
    }

    [Fact]
    public void EveryDiscardedTask_IsGuarded()
    {
        var unguarded = AppFiles()
            .SelectMany(path => File.ReadLines(path).Where(line => !line.TrimStart().StartsWith("//")).SelectMany(line => Discard.Matches(line)
                .Select(match => match.Groups["call"].Value.Split('.', ' ', '(').Last())
                .Where(call => call != "RunGuardedAsync" && !NeedNoGuard.ContainsKey(call))
                .Select(call => $"{Path.GetFileName(path)}: {call}")))
            .ToList();

        unguarded.ShouldBeEmpty("start it with _ = RunGuardedAsync(() => ..., \"Name\")");
    }
}
