using AnimationEditor.Core.Paths;
using Avalonia.Controls;
using System;
using System.ComponentModel;

namespace AnimationEditor.App.Services;

/// <summary>
/// The right-click menu on the texture, shared by the wireframe and preview canvases: "Reveal &lt;file&gt; in File
/// Manager" and "Open &lt;file&gt; in New Tab". Only the path source differs between the two.
/// </summary>
internal static class TextureContextMenu
{
    /// <summary>
    /// Clears <paramref name="menu"/> and fills it for <paramref name="absPath"/>, or cancels the opening when there is
    /// no texture. The open item is left out when <paramref name="openInNewTab"/> is null (no host wired it).
    /// </summary>
    public static void Populate(
        ContextMenu? menu,
        string? absPath,
        Action<string>? showError,
        Action<string>? openInNewTab,
        CancelEventArgs e)
    {
        if (menu is null || absPath is null)
        {
            e.Cancel = true;
            return;
        }

        menu.Items.Clear();
        string fileName = new FilePath(absPath).NoPath;
        var reveal = new MenuItem { Header = $"Reveal {fileName} in File Manager" };
        reveal.Click += (_, _) =>
        {
            var error = ShellExplorer.RevealFile(absPath);
            if (error is not null) showError?.Invoke(error);
        };
        menu.Items.Add(reveal);

        if (openInNewTab is not null)
        {
            var open = new MenuItem { Header = $"Open {fileName} in New Tab" };
            open.Click += (_, _) => openInNewTab(absPath);
            menu.Items.Add(open);
        }
    }
}
