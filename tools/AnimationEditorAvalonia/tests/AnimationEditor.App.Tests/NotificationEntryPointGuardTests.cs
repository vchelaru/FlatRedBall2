using System.Text.RegularExpressions;
using AnimationEditor.Views.Controls;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The editor has two pop-up kinds, INFO (top-right toast) and PROBLEM (top-centre banner), reached only through
/// <c>ShowInfo</c> and <c>ShowProblem</c>. This fails when a third kind or an old entry point comes back.
/// </summary>
public class NotificationEntryPointGuardTests
{
    private static readonly string[] TwoEntryPoints = { "ShowInfo", "ShowProblem" };

    private static string SourceRoot()
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string candidate = Path.Combine(dir, "tools", "AnimationEditorAvalonia", "src");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("Could not find tools/AnimationEditorAvalonia/src above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<(string Path, string Text)> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => (path, File.ReadAllText(path)));

    [Fact]
    public void TheOverlay_OffersExactlyShowInfoAndShowProblem_ForNotifications()
    {
        var shown = typeof(EditorNotificationOverlay)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Where(name => name.StartsWith("Show", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        // ShowItemDeleted is the info kind with an Undo button, not a third kind.
        shown.ShouldBe(new[] { "ShowInfo", "ShowItemDeleted", "ShowProblem" });
    }

    [Fact]
    public void NoCode_CallsAnOldNotificationEntryPoint()
    {
        var old = new Regex(@"\b(ShowToast|ShowErrorBanner|ShowWarningBanner|ShowItemDeletedToast)\b");
        var offenders = SourceFiles()
            .Where(file => old.IsMatch(file.Text))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        offenders.ShouldBeEmpty("use ShowInfo (needs nothing from the user) or ShowProblem (not saved, or an asked action failed)");
    }

    [Fact]
    public void OutsideTheOverlay_NotificationsAreReachedOnlyThroughTheEntryPoints()
    {
        var access = new Regex(@"\bNotifications\.(\w+)");
        var allowed = new HashSet<string>(TwoEntryPoints) { "ShowItemDeleted", "WireUndo" };
        var offenders = SourceFiles()
            .SelectMany(file => access.Matches(file.Text).Select(match => (File: Path.GetFileName(file.Path), Member: match.Groups[1].Value)))
            .Where(use => !allowed.Contains(use.Member))
            .Select(use => $"{use.File}: Notifications.{use.Member}")
            .Distinct()
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void TheStatusBarMessage_NoLongerDoublesAsAnErrorPopup()
    {
        // isError: true used to route a status message to the banner; a problem now says so by calling ShowProblem.
        var offenders = SourceFiles()
            .Where(file => Regex.IsMatch(file.Text, @"ShowStatusMessage\([^;]*isError"))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void AppCommands_NamesItsNotificationEventBySeverity_NotAnUntypedNotified()
    {
        typeof(AnimationEditor.Core.CommandsAndState.IAppCommands).GetEvent("Notified").ShouldBeNull();
        typeof(AnimationEditor.Core.CommandsAndState.IAppCommands).GetEvent("InfoRaised").ShouldNotBeNull();
    }
}
