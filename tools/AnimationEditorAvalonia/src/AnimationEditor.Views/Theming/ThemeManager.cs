using AnimationEditor.Core.Models;
using Avalonia.Styling;

namespace AnimationEditor.App.Theming;

/// <summary>
/// Maps the persisted <see cref="AppTheme"/> choice onto Avalonia's <see cref="ThemeVariant"/>.
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// Converts an <see cref="AppTheme"/> to the <see cref="ThemeVariant"/> to assign to
    /// <c>Application.RequestedThemeVariant</c>. <see cref="AppTheme.System"/> maps to
    /// <see cref="ThemeVariant.Default"/>, which makes Avalonia follow the OS setting.
    /// </summary>
    public static ThemeVariant ToVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
