using Avalonia;
using Avalonia.Themes.Fluent;

namespace AnimationEditor.Views.Tests;

/// <summary>Minimal headless-test Application: just enough theme to render stock controls.</summary>
public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
