using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Issue #839 follow-up: the app's autosave policy -- any edit (raised as
/// <c>IApplicationEvents.AnimationChainsChanged</c> from ~20 <c>IUndoableCommand</c> types and a
/// couple of direct <c>AppCommands</c> call sites) writes the <c>.achx</c> to disk -- used to be
/// wired only in <c>MainWindow</c> (the Avalonia app layer). That made it untestable without a
/// full headless window + WireframeControl, and it was misdiagnosed twice while investigating
/// this issue as a result. It now lives in <c>AppCommands</c>'s own constructor, so these tests
/// exercise it directly with no Avalonia/UI involved at all.
/// </summary>
public class AppCommandsSaveOnChangeTests
{
    [Fact]
    public void RaiseAnimationChainsChanged_FileNameSet_SavesToDisk()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var tmpPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".achx");
        ctx.ProjectManager.FileName = tmpPath;

        try
        {
            ctx.ApplicationEvents.RaiseAnimationChainsChanged();

            Assert.True(File.Exists(tmpPath), $"Expected {tmpPath} to be written by the autosave policy.");
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    [Fact]
    public void RaiseAnimationChainsChanged_NoFileNameSet_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = null;

        var ex = Record.Exception(() => ctx.ApplicationEvents.RaiseAnimationChainsChanged());

        Assert.Null(ex);
    }
}
