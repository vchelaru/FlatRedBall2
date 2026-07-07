namespace AnimationEditor.Core.IO;

/// <summary>
/// A minimal key/value string store, abstracting the browser's <c>window.localStorage</c> (the
/// only platform this is for -- desktop persists <see cref="Models.AppSettingsModel"/> to a real
/// settings file instead, via <see cref="AppSettingsLocation"/>). Kept this narrow so the
/// persistence logic in <see cref="BrowserSettingsStore"/> is testable with a fake, independent
/// of the actual browser JS interop.
/// </summary>
public interface ILocalStorage
{
    /// <summary>Returns the stored value for <paramref name="key"/>, or <c>null</c> if unset.</summary>
    string? GetItem(string key);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, overwriting any existing value.</summary>
    void SetItem(string key, string value);
}
