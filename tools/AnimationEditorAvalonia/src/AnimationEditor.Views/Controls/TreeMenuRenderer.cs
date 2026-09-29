using AnimationEditor.Core.CommandsAndState;
using Avalonia.Controls;
using Avalonia.Layout;
using SvgIcon = Avalonia.Svg.Skia.Svg;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Turns a <see cref="TreeMenuPlanBuilder"/> plan into Avalonia menu items. Shared by desktop's
/// MainWindow and the browser's <see cref="AnimationTreeControl"/>; each host supplies only the
/// items for its own <see cref="TreeMenuHostSlot"/> placeholders.
/// </summary>
public static class TreeMenuRenderer
{
    public static void Render(IReadOnlyList<TreeMenuItem> plan, ItemCollection target,
        Action<TreeMenuHostSlot> addHostSlotItem)
    {
        foreach (var entry in plan)
        {
            if (entry.IsSeparator)
                target.Add(new Separator());
            else if (entry.HostSlot is { } slot)
                addHostSlotItem(slot);
            else
                target.Add(CreateMenuItem(entry));
        }
    }

    public static MenuItem CreateMenuItem(string header, Action onClick, TreeMenuIcon? icon = null)
    {
        var item = new MenuItem { Header = header };
        if (icon is { } i)
            item.Icon = CreateIcon(i);
        item.Click += (_, _) => onClick();
        return item;
    }

    public static string IconAssetPath(TreeMenuIcon icon) => icon switch
    {
        // The tree view shows rectangles with the generic shape icon, so the menu matches it.
        TreeMenuIcon.Rectangle => "avares://AnimationEditor.Views/Assets/icons/svg/IconShape.svg",
        TreeMenuIcon.Circle    => "avares://AnimationEditor.Views/Assets/icons/svg/IconCircle.svg",
        TreeMenuIcon.Polygon   => "avares://AnimationEditor.Views/Assets/icons/svg/IconPolygon.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
    };

    private static MenuItem CreateMenuItem(TreeMenuItem entry)
    {
        if (entry.Children is not { } children)
            return CreateMenuItem(entry.Header!, entry.OnClick!, entry.Icon);

        var parent = new MenuItem { Header = entry.Header };
        if (entry.Icon is { } icon)
            parent.Icon = CreateIcon(icon);
        foreach (var child in children)
            parent.Items.Add(CreateMenuItem(child));
        return parent;
    }

    // Same size and theme token as the tree view's node icons (MainWindow.axaml).
    private static SvgIcon CreateIcon(TreeMenuIcon icon)
    {
        var svg = new SvgIcon(baseUri: null!)
        {
            Width = 14,
            Height = 14,
            Path = IconAssetPath(icon),
            VerticalAlignment = VerticalAlignment.Center,
        };
        svg.Bind(SvgIcon.CurrentColorProperty, svg.GetResourceObservable("IconInkDim"));
        return svg;
    }
}
