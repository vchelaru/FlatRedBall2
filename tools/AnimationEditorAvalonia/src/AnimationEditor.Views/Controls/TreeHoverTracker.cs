using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Reports the model object (<see cref="TreeNodeVm.Data"/>) of the tree row under the pointer,
/// or null once the pointer is over no row or leaves the tree (#1216). Shared by the desktop
/// tree and the browser's <see cref="AnimationTreeControl"/> so both feed the wireframe's hover
/// highlight the same way.
/// </summary>
public static class TreeHoverTracker
{
    /// <summary>
    /// Wires <paramref name="tree"/> to call <paramref name="onHoverChanged"/> whenever the
    /// hovered row's data changes. The innermost <see cref="TreeViewItem"/> wins, so a frame row
    /// reports the frame, not its parent chain.
    /// </summary>
    public static void Attach(TreeView tree, Action<object?> onHoverChanged)
    {
        object? current = null;

        void Report(object? data)
        {
            if (ReferenceEquals(data, current)) return;
            current = data;
            onHoverChanged(data);
        }

        // handledEventsToo: the drag-reorder handlers mark moves handled while a drag is armed.
        tree.AddHandler(
            InputElement.PointerMovedEvent,
            (_, e) => Report(((e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext as TreeNodeVm)?.Data),
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        tree.PointerExited += (_, _) => Report(null);
    }
}
