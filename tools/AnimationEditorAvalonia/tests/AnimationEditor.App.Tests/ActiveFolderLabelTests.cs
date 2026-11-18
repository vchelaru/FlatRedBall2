using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #944: a small-font label above the sidebar tabs shows which folder is open, since neither the
/// Images nor Animations tab otherwise displays it.
/// </summary>
public class ActiveFolderLabelTests
{
    [AvaloniaFact]
    public void ActiveFolderLabel_NoFolderOpen_ShowsPlaceholder()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var label = window.FindControl<TextBlock>("ActiveFolderLabel");
            Assert.NotNull(label);
            Assert.Equal("No folder open", label!.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ActiveFolderLabel_AfterOpeningProjectFolder_ShowsFolderPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("ActiveFolderLabel");
            Assert.NotNull(label);
            Assert.Equal(dir, label!.Text);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
