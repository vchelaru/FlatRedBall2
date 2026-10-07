using System;
using Avalonia.Platform;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #644: shared SVG icon assets.
public class IconAssetsTests
{
    private const string SampleIconUri =
        "avares://AnimationEditor.Views/Assets/icons/svg/IconPlay.svg";

    [AvaloniaFact]
    public void SharedSvgIcon_IsLoadableFromViewsAssembly()
    {
        using var stream = AssetLoader.Open(new Uri(SampleIconUri));
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }
}
