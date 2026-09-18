using System;
using System.IO;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Exercises <see cref="TiledExtensionInstaller"/>'s file-IO surface (Install/GetStatus/
/// ValidateFolder) against a real temp directory. Unlike <c>WindowsFileAssociationService</c>'s
/// registry wiring, these methods take the target folder as a parameter rather than reading it
/// from the OS, so they're fully testable without touching real Tiled paths. Only
/// <see cref="ITiledExtensionInstaller.DetectExtensionsFolder"/> (reads special-folder env vars)
/// is thin, untested wiring, per CLAUDE.md's hard-to-test-surface guidance.
/// </summary>
public class TiledExtensionInstallerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "TiledExtensionInstallerTests", Guid.NewGuid().ToString("N"));

    private readonly TiledExtensionInstaller _installer = new();

    public TiledExtensionInstallerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetStatus_AfterInstall_ReturnsUpToDate()
    {
        _installer.Install(_tempDir);

        var status = _installer.GetStatus(_tempDir);

        Assert.Equal(TiledExtensionInstallStatus.UpToDate, status);
    }

    [Fact]
    public void GetStatus_FilesModifiedAfterInstall_ReturnsOutdated()
    {
        _installer.Install(_tempDir);
        string mapperPath = Path.Combine(_tempDir, TiledExtensionPaths.SubfolderName, TiledExtensionPaths.MapperFileName);
        File.WriteAllText(mapperPath, "// hand-edited");

        var status = _installer.GetStatus(_tempDir);

        Assert.Equal(TiledExtensionInstallStatus.Outdated, status);
    }

    [Fact]
    public void GetStatus_NothingInstalled_ReturnsNotInstalled()
    {
        var status = _installer.GetStatus(_tempDir);

        Assert.Equal(TiledExtensionInstallStatus.NotInstalled, status);
    }

    [Fact]
    public void Install_TargetBlockedByExistingFile_ReturnsErrorMessage()
    {
        // A file sitting where the "tiled-achj-import" subfolder needs to go makes
        // Directory.CreateDirectory throw -- this exercises the permission/IO failure path
        // without touching real filesystem ACLs.
        string blockingFilePath = Path.Combine(_tempDir, TiledExtensionPaths.SubfolderName);
        File.WriteAllText(blockingFilePath, string.Empty);

        string? error = _installer.Install(_tempDir);

        Assert.NotNull(error);
    }

    [Fact]
    public void Install_ValidFolder_WritesBothFiles()
    {
        string? error = _installer.Install(_tempDir);

        Assert.Null(error);
        string subfolder = Path.Combine(_tempDir, TiledExtensionPaths.SubfolderName);
        Assert.True(File.Exists(Path.Combine(subfolder, TiledExtensionPaths.MapperFileName)));
        Assert.True(File.Exists(Path.Combine(subfolder, TiledExtensionPaths.ImportFileName)));
    }

    [Fact]
    public void ValidateFolder_BlockedByExistingFile_ReturnsErrorMessage()
    {
        string blockingFilePath = Path.Combine(_tempDir, "not-a-folder");
        File.WriteAllText(blockingFilePath, string.Empty);

        string? error = _installer.ValidateFolder(blockingFilePath);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateFolder_EmptyPath_ReturnsErrorMessage()
    {
        string? error = _installer.ValidateFolder(string.Empty);

        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateFolder_WritableExistingFolder_ReturnsNull()
    {
        string? error = _installer.ValidateFolder(_tempDir);

        Assert.Null(error);
    }
}
