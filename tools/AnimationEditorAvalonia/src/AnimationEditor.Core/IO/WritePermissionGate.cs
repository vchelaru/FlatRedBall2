using System;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure mapping of File System Access API permission state strings
/// (<c>granted</c>/<c>denied</c>/<c>prompt</c>/diagnostics) to whether Save-without-dialog is
/// allowed. Kept free of JS/Avalonia so the gate is unit-testable; browser wiring only feeds
/// the string from <c>queryPermission</c>/<c>requestPermission</c>.
/// </summary>
public static class WritePermissionGate
{
    public const string Granted = "granted";

    /// <summary>True only when the browser reported an explicit write grant.</summary>
    public static bool AllowsDirectSave(string? permissionState) =>
        string.Equals(permissionState, Granted, StringComparison.Ordinal);

    /// <summary>Status-bar fragment after Open Folder, e.g. <c>[write:granted]</c>.</summary>
    public static string FormatStatusSuffix(string? permissionState) =>
        $"[write:{permissionState ?? "unknown"}]";

    /// <summary>Error diagnostic when Save refuses a known handle.</summary>
    public static string FormatSaveFailure(string? permissionState) =>
        $"write permission: {permissionState ?? "unknown"}";

    /// <summary>
    /// Save uses pick-time write grant only (<c>queryPermission</c>). Do not call
    /// <c>requestPermission</c> from the Save menu — WASM loses user activation and Chrome
    /// returns <c>denied</c> with no prompt.
    /// </summary>
    public static (bool CanSave, string? FailureDiagnostic) EvaluateSaveFromQueryState(string? queryState) =>
        AllowsDirectSave(queryState)
            ? (true, null)
            : (false, FormatSaveFailure(queryState));
}
