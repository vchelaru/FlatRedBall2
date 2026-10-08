using System;
using System.Linq;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Xunit;
using SvgIcon = Avalonia.Svg.Skia.Svg;

namespace AnimationEditor.Views.Tests;

public class TreeMenuRendererTests
{
    [AvaloniaFact]
    public void Render_ItemWithIcon_SetsSvgIconFromSharedAssets()
    {
        var target = new ContextMenu();
        var plan = new[] { TreeMenuItem.Item("Add Circle", () => { }, TreeMenuIcon.Circle) };

        TreeMenuRenderer.Render(plan, target.Items, _ => { });

        var svg = Assert.IsType<SvgIcon>(((MenuItem)target.Items.Single()!).Icon);
        Assert.Equal("avares://AnimationEditor.Views/Assets/icons/svg/IconCircle.svg", svg.Path);
    }

    [AvaloniaFact]
    public void IconAssetPath_EveryIcon_ResolvesToAnExistingAsset()
    {
        foreach (var icon in Enum.GetValues<TreeMenuIcon>())
            Assert.True(AssetLoader.Exists(new Uri(TreeMenuRenderer.IconAssetPath(icon))), icon.ToString());
    }
}
