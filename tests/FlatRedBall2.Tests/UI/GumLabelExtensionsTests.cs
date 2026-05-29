using System;
using FlatRedBall2.UI;
using Gum.Forms.Controls;
using MonoGameGum.GueDeriving;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.UI;

public class GumLabelExtensionsTests
{
    private static Label CreateLabelWithVisual() => new Label(new TextRuntime());

    [Fact]
    public void SetFontSize_Label_UpdatesVisualFontSize()
    {
        var label = CreateLabelWithVisual();

        label.SetFontSize(28);

        var text = label.Visual as TextRuntime;
        text.ShouldNotBeNull();
        text.FontSize.ShouldBe(28);
    }

    [Fact]
    public void SetOpacity_Label_UpdatesVisualAlpha()
    {
        var label = CreateLabelWithVisual();

        label.SetOpacity(0.5f);

        var text = label.Visual as TextRuntime;
        text.ShouldNotBeNull();
        text.Alpha.ShouldBe(128);
    }

    [Fact]
    public void SetOpacity_OpacityOutsideUnitRange_ThrowsArgumentOutOfRangeException()
    {
        var label = CreateLabelWithVisual();

        Should.Throw<ArgumentOutOfRangeException>(() => label.SetOpacity(1.1f));
    }
}
