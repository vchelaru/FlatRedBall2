using AnimationEditor.Core.IO;
using AnimationEditor.Tests.Shared;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>A folder in the tree that cannot be listed (every Windows drive root has one).</summary>
public class PngFolderScannerUnreadableFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ae-unreadable").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ListFiles_SubfolderWithoutReadAccess_ListsTheRest()
    {
        File.WriteAllBytes(Path.Combine(_root, "sheet.png"), [1]);
        using var denied = new UnlistableFolder(Path.Combine(_root, "denied"));

        PngFolderScanner.ListFiles(_root).Select(file => file.RelativePath).ShouldBe(new[] { "sheet.png" });
    }
}
