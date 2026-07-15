namespace AnimationEditor.Core.IO;

/// <summary>
/// Formats a user-facing message for a failed Open Folder load. JS-interop directory reads
/// (enumeration, file access) can fail for reasons outside this app's control -- revoked
/// permission, a moved/locked folder, or (confirmed live 2026-07-15, exact mechanism unconfirmed)
/// security software blocking directory <em>listing</em> specifically while still allowing
/// named-file access on the same handle. These must never crash the whole WASM runtime -- an
/// unhandled exception here aborts the entire single-threaded Mono runtime, not just the failed
/// operation (same root cause the reflection-disabled JSON crash had). Catch at the call site
/// and surface this message instead of letting the exception propagate.
/// </summary>
public static class OpenFolderLoadFailure
{
    public static string FormatMessage(string folderName, string? diagnostic) =>
        $"Couldn't read folder \"{folderName}\" ({diagnostic ?? "unknown error"}). " +
        "This can happen if antivirus/security software is blocking folder access. " +
        "Try allowing this app, or pick a different folder.";
}
