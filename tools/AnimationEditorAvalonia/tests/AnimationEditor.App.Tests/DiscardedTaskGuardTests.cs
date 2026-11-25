using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #2057 guard: a command that starts a task and does not await it loses what the task throws, so it goes through
/// <c>MainWindow.RunGuardedAsync</c>, which says it in the problem banner.
/// </summary>
public class DiscardedTaskGuardTests
{
    private static readonly Regex Discard = new(@"(?<![\w])_ = (?<call>[^;]*?Async)\(");

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

    [Fact]
    public void EveryDiscardedTask_IsGuarded()
    {
        var unguarded = Directory.EnumerateFiles(AppSource(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => File.ReadLines(path).Where(line => !line.TrimStart().StartsWith("//")).SelectMany(line => Discard.Matches(line)
                .Select(match => match.Groups["call"].Value.Split('.', ' ', '(').Last())
                .Where(call => call != "RunGuardedAsync" && !NeedNoGuard.ContainsKey(call))
                .Select(call => $"{Path.GetFileName(path)}: {call}")))
            .ToList();

        unguarded.ShouldBeEmpty("start it with _ = RunGuardedAsync(() => ..., \"Name\")");
    }
}
