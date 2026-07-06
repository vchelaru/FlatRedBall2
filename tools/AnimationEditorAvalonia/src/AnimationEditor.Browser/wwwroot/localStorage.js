// #610: minimal window.localStorage bridge for System.Runtime.InteropServices.JavaScript's
// JSImport, imported explicitly (JSHost.ImportAsync) rather than auto-loaded like main.js --
// see LocalStorageInterop.cs. Kept to exactly the two operations BrowserSettingsStore needs.
export function getItem(key) {
    return window.localStorage.getItem(key);
}

export function setItem(key, value) {
    window.localStorage.setItem(key, value);
}
