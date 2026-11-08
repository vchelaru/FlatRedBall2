using AnimationEditor.App.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// An exception thrown from <c>MainWindow</c>'s Closed handler skips the handlers after it, including the one that
/// tells Avalonia's desktop lifetime the window is gone, and on Windows it also takes the whole process down with a
/// fatal callback exception. Cleanup that touches the file system must therefore never throw out of Closed.
/// </summary>
public class WindowCloseRobustnessTests
{
    private static string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [AvaloniaFact]
    public void Close_WithAnUndeletableRecoveryFile_DoesNotThrow()
    {
        string folder = NewFolder();
        string recovery = Path.Combine(folder, "recovery.achx");
        File.WriteAllText(recovery, "x");
        File.SetAttributes(recovery, FileAttributes.ReadOnly);
        MainWindow window = App.CreateDetachedWindow(folder, recovery);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Should.NotThrow(() => window.Close());
        }
        finally
        {
            File.SetAttributes(recovery, FileAttributes.Normal);
        }
    }

    [AvaloniaFact]
    public void Close_WithAReadOnlySettingsFile_DoesNotThrow()
    {
        string folder = NewFolder();
        string recovery = Path.Combine(folder, "recovery.achx");
        MainWindow window = App.CreateDetachedWindow(folder, recovery);
        string settings = Path.Combine(folder, "AnimationEditor", "AESettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "{}");
        File.SetAttributes(settings, FileAttributes.ReadOnly);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Should.NotThrow(() => window.Close());
        }
        finally
        {
            File.SetAttributes(settings, FileAttributes.Normal);
        }
    }

    [Fact]
    public void CloseCleanup_LaterStepsRunWhenAnEarlierStepThrows_AndTheFailureIsReported()
    {
        var ran = new List<string>();
        var errors = new List<Exception>();

        CloseCleanup.Run(
            [() => ran.Add("a"), () => throw new IOException("locked"), () => ran.Add("c")],
            errors.Add);

        ran.ShouldBe(["a", "c"]);
        errors.Count.ShouldBe(1);
    }
}
