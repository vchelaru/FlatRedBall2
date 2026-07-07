using System;
using AnimationEditor.Core.Models;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Persists browser-editor settings to <see cref="ILocalStorage"/> -- the browser-build
/// counterpart to desktop's file-backed <see cref="Models.AppSettingsModel"/> (see
/// <see cref="AppSettingsLocation"/>). Only covers the settings the browser build actually has UI
/// for; extend this alongside whatever new setting gets its first browser control, rather than
/// persisting fields nothing can set yet.
/// </summary>
public class BrowserSettingsStore
{
    private const string ThemeKey = "ae.theme";

    private readonly ILocalStorage _storage;

    public BrowserSettingsStore(ILocalStorage storage) => _storage = storage;

    /// <summary>
    /// Returns the persisted theme, or <c>null</c> if nothing was ever saved (fresh
    /// browser/cleared storage) or the stored value isn't a recognized <see cref="AppTheme"/>
    /// name -- callers should fall back to a sensible default rather than throwing.
    /// </summary>
    public AppTheme? LoadTheme()
    {
        var raw = _storage.GetItem(ThemeKey);
        return raw is not null && Enum.TryParse<AppTheme>(raw, out var theme) ? theme : null;
    }

    public void SaveTheme(AppTheme theme) => _storage.SetItem(ThemeKey, theme.ToString());
}
