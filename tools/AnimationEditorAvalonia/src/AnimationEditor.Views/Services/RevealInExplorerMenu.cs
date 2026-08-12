using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using System;
using System.ComponentModel;

namespace AnimationEditor.App.Services;

/// <summary>
/// Shared "View &lt;file&gt; in Explorer" context-menu population for the wireframe and preview
/// canvases (#573 / #695 A6). Both controls open the same one-item menu; only the path source
/// differs.
/// </summary>
internal static class RevealInExplorerMenu
{
    /// <summary>
    /// Clears <paramref name="menu"/> and adds a single reveal item for <paramref name="absPath"/>,
    /// or cancels the opening when there is nothing to reveal.
    /// </summary>
    public static void Populate(
        ContextMenu? menu,
        string? absPath,
        Action<string>? showError,
        CancelEventArgs e)
    {
        if (menu is null || absPath is null)
        {
            e.Cancel = true;
            return;
        }

        menu.Items.Clear();
        var item = new MenuItem { Header = $"View {new FilePath(absPath).NoPath} in Explorer" };
        item.Click += (_, _) =>
        {
            var error = ShellExplorer.RevealFile(absPath);
            if (error is not null) showError?.Invoke(error);
        };
        menu.Items.Add(item);
    }
}
