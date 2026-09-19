using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Browser-build counterpart to desktop's file-backed <see cref="IoManager"/> -- persists the
/// same <c>.aeproperties</c> companion file (via <see cref="ICompanionFileStore"/>, backed by a
/// <c>FileSystemDirectoryHandle</c> on the real browser build) instead of writing straight to
/// disk. Portable and unit-tested with a fake store, same split as
/// <see cref="BrowserSettingsStore"/>/<see cref="ILocalStorage"/>; the real
/// <see cref="ICompanionFileStore"/> (JS interop) lives in <c>AnimationEditor.Browser</c>.
/// <para>
/// <see cref="ICompanionFileStore"/> is inherently async (no synchronous
/// <c>FileSystemDirectoryHandle</c> API exists), but <see cref="IIoManager"/>'s save/load methods
/// are synchronous (desktop's disk I/O is fast enough to block on) -- every method here fires the
/// real work off as fire-and-forget and reports failures through <see cref="SaveFailed"/> /
/// <see cref="SettingsLoaded"/> once it completes, rather than blocking the caller. This also
/// means <see cref="TryLoadCompanionSettings"/> can never honor its synchronous contract on this
/// implementation -- see its doc comment.
/// </para>
/// </summary>
public class BrowserIoManager : IIoManager
{
    private readonly IAppState _appState;
    private readonly ICompanionFileStore _store;

    public BrowserIoManager(IAppState appState, ICompanionFileStore store)
    {
        _appState = appState;
        _store = store;
    }

    public event Action<string, Exception>? SaveFailed;
    public event Action<AESettingsSave>? SettingsLoaded;

    // The browser has no local-temp-file crash-recovery story yet (no filesystem outside a
    // user-granted directory handle) -- these members exist only to satisfy IIoManager and are
    // no-ops. Revisit if/when browser recovery is designed.
    public string RecoveryFilePath { get; set; } = string.Empty;
    public void WriteRecoveryFile(AnimationChainListSave? animationChainListSave) { }
    public void DeleteRecoveryFile() { }
    public bool RecoveryFileExists() => false;
    public AnimationChainListSave? TryReadRecoveryFile() => null;

    /// <summary>
    /// Computes the companion file's bare name from an .achx file name, dropping any directory
    /// component -- matches desktop's extension swap ("hero.achx" -&gt; "hero.aeproperties") but a
    /// bare name rather than a full path, since a browser <c>FileSystemDirectoryHandle</c>
    /// addresses files by name within one flat folder, never a path.
    /// </summary>
    internal static string GetCompanionFileName(FilePath achxFile)
    {
        var bareName = achxFile.NoPath;
        var bareNameNoExtension = new FilePath(bareName).RemoveExtension().Original ?? bareName;
        return bareNameNoExtension + ".aeproperties";
    }

    /// <summary>Same bare-name extension swap as <see cref="GetCompanionFileName"/>, but for the
    /// <c>.tiledsync</c> companion file (see <see cref="AETiledSyncSave"/>).</summary>
    internal static string GetTiledSyncCompanionFileName(FilePath achxFile)
    {
        var bareName = achxFile.NoPath;
        var bareNameNoExtension = new FilePath(bareName).RemoveExtension().Original ?? bareName;
        return bareNameNoExtension + ".tiledsync";
    }

    public void SaveCompanionFileFor(FilePath fileName, AESettingsSave settings)
    {
        var companionName = GetCompanionFileName(fileName);
        _ = SaveAsync(companionName, settings);
    }

    private async Task SaveAsync(string companionName, AESettingsSave settings)
    {
        try
        {
            XmlFile.SerializeToString(settings, out var xml);
            await _store.WriteAsync(companionName, xml);
        }
        catch (Exception e)
        {
            SaveFailed?.Invoke("Could not save companion file " + companionName + "\n\n" + e, e);
        }
    }

    public void LoadAndApplyCompanionFileFor(string achxFile)
    {
        _ = LoadAndApplyAsync(achxFile);
    }

    private async Task LoadAndApplyAsync(string achxFile)
    {
        var settings = await TryLoadCompanionSettingsAsync(achxFile);
        if (settings != null)
        {
            ApplySettings(settings);
        }
    }

    /// <summary>
    /// Always returns <c>null</c> -- reading a companion file requires an async round trip through
    /// the browser's <c>FileSystemDirectoryHandle</c>, which this synchronous method has no way to
    /// wait on without risking a deadlock (WASM is single-threaded; blocking here would starve the
    /// same thread the read needs to complete on). Callers on the browser build that need this
    /// data (e.g. to avoid a collapse-then-restore flicker) should call
    /// <see cref="TryLoadCompanionSettingsAsync"/> directly instead of going through
    /// <see cref="IIoManager"/>.
    /// </summary>
    public AESettingsSave? TryLoadCompanionSettings(string achxFile) => null;

    /// <summary>Async counterpart to <see cref="TryLoadCompanionSettings"/> -- the real way to read
    /// a companion file on the browser build. Returns <c>null</c> when no companion file exists or
    /// it fails to deserialize.</summary>
    public async Task<AESettingsSave?> TryLoadCompanionSettingsAsync(string achxFile)
    {
        var companionName = GetCompanionFileName(new FilePath(achxFile));
        var xml = await _store.TryReadAsync(companionName);
        if (xml is null) return null;

        try
        {
            return XmlFile.DeserializeFromString<AESettingsSave>(xml);
        }
        catch
        {
            return null;
        }
    }

    // Running the actual sync (DotTiled reads/writes real .tsx files; see
    // AnimationEditor.Core/Tiled/) is desktop-only, but the association itself is still stored
    // here so it round-trips if a project is later opened on desktop -- same store-backed pattern
    // as the .aeproperties methods above.

    /// <summary>
    /// Always returns empty -- reading the <c>.tiledsync</c> companion file requires an async
    /// round trip through <see cref="ICompanionFileStore"/>, which this synchronous method has no
    /// way to wait on (see <see cref="TryLoadCompanionSettings"/> for the same limitation).
    /// </summary>
    public IReadOnlyList<string> GetAssociatedTiledTilesetPaths(string achxFile) => Array.Empty<string>();

    public void AddAssociatedTiledTilesetPath(string achxFile, string tsxFile)
    {
        _ = AddAssociatedTiledTilesetPathAsync(achxFile, tsxFile);
    }

    private async Task AddAssociatedTiledTilesetPathAsync(string achxFile, string tsxFile)
    {
        try
        {
            var achxFilePath = new FilePath(achxFile);
            var achxFolder = achxFilePath.GetDirectoryContainingThis();
            var relativeTsxPath = new FilePath(tsxFile).RelativeTo(achxFolder);

            var companionName = GetTiledSyncCompanionFileName(achxFilePath);
            var existingJson = await _store.TryReadAsync(companionName);
            var settings = existingJson is null
                ? new AETiledSyncSave()
                : JsonSerializer.Deserialize(existingJson, AETiledSyncJsonContext.Default.AETiledSyncSave)
                    ?? new AETiledSyncSave();

            var alreadyAssociated = settings.TiledTilesetPaths
                .Any(p => new FilePath(achxFolder.FullPath + p) == new FilePath(tsxFile));
            if (!alreadyAssociated)
            {
                settings.TiledTilesetPaths.Add(relativeTsxPath);
                var json = JsonSerializer.Serialize(settings, AETiledSyncJsonContext.Default.AETiledSyncSave);
                await _store.WriteAsync(companionName, json);
            }
        }
        catch (Exception e)
        {
            SaveFailed?.Invoke("Could not save Tiled sync companion file " + achxFile + "\n\n" + e, e);
        }
    }

    private void ApplySettings(AESettingsSave settings)
    {
        _appState.IsSnapToGridChecked = settings.SnapToGrid;
        _appState.GridSize = settings.GridSize;
        SettingsLoaded?.Invoke(settings);
    }
}
