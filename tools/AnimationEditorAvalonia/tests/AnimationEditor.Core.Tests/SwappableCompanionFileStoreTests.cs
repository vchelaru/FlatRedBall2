using AnimationEditor.Core.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

// #754 Phase B: the browser build has no companion-file store to write to until a real folder is
// opened (Open Folder grants a FileSystemDirectoryHandle; the bundled sample and drag-dropped
// loose files never do). SwappableCompanionFileStore lets BrowserIoManager be constructed once at
// startup and have its real backing store plugged in later, once one exists.
public class SwappableCompanionFileStoreTests
{
    private sealed class FakeInnerStore : ICompanionFileStore
    {
        public string? WrittenFileName;
        public string? WrittenContents;
        public string? ToReturn;

        public Task WriteAsync(string fileName, string contents)
        {
            WrittenFileName = fileName;
            WrittenContents = contents;
            return Task.CompletedTask;
        }

        public Task<string?> TryReadAsync(string fileName) => Task.FromResult(ToReturn);
    }

    [Fact]
    public async Task TryReadAsync_NoInner_ReturnsNull()
    {
        var store = new SwappableCompanionFileStore();

        var result = await store.TryReadAsync("hero.aeproperties");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryReadAsync_WithInner_ForwardsToInner()
    {
        var store = new SwappableCompanionFileStore { Inner = new FakeInnerStore { ToReturn = "<xml/>" } };

        var result = await store.TryReadAsync("hero.aeproperties");

        Assert.Equal("<xml/>", result);
    }

    [Fact]
    public async Task WriteAsync_NoInner_DoesNotThrow()
    {
        var store = new SwappableCompanionFileStore();

        await store.WriteAsync("hero.aeproperties", "<xml/>");
    }

    [Fact]
    public async Task WriteAsync_WithInner_ForwardsToInner()
    {
        var inner = new FakeInnerStore();
        var store = new SwappableCompanionFileStore { Inner = inner };

        await store.WriteAsync("hero.aeproperties", "<xml/>");

        Assert.Equal("hero.aeproperties", inner.WrittenFileName);
        Assert.Equal("<xml/>", inner.WrittenContents);
    }
}
