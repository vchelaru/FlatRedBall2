using System;
using System.IO;
using System.Threading.Tasks;
using FlatRedBall2.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.IO;

// What Initialize does with EngineInitSettings.StorageName / StorageRootDirectory, via the internal
// ConfigureStorage it calls (a real Initialize needs a window).
public class StorageConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "frb2-storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ConfigureStorage_NullSettings_FirstCallThrowsNamingTheSetting()
    {
        var engine = new FlatRedBallService();

        engine.ConfigureStorage(null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.ExistsAsync("a"));
        ex.Message.ShouldContain("StorageName");
    }

    [Fact]
    public async Task ConfigureStorage_NoName_EveryOperationThrowsInvalidOperation()
    {
        var engine = new FlatRedBallService();
        engine.ConfigureStorage(new EngineInitSettings { StorageRootDirectory = _root });

        await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.ReadTextAsync("a"));
        await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.WriteTextAsync("a", "x"));
        await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.OpenReadAsync("a"));
        await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.DeleteAsync("a"));
        await Should.ThrowAsync<InvalidOperationException>(() => engine.Storage.ListAsync());
        Directory.Exists(_root).ShouldBeFalse();
    }

    [Fact]
    public void ConfigureStorage_NoName_IsNotPersistentAndDoesNotThrow()
    {
        var engine = new FlatRedBallService();

        engine.ConfigureStorage(null);

        engine.Storage.IsPersistent.ShouldBeFalse();
    }

    [Fact]
    public async Task ConfigureStorage_NameAndRootOverride_StoresUnderTheOverride()
    {
        var engine = new FlatRedBallService();
        engine.ConfigureStorage(new EngineInitSettings { StorageName = "my-game", StorageRootDirectory = _root });

        await engine.Storage.WriteTextAsync("saves/one.json", "x");

        engine.Storage.IsPersistent.ShouldBeTrue();
        File.ReadAllText(Path.Combine(_root, "saves", "one.json")).ShouldBe("x");
    }

    [Fact]
    public void ConfigureStorage_NameOnly_UsesDefaultRootWithoutTouchingDisk()
    {
        var engine = new FlatRedBallService();

        engine.ConfigureStorage(new EngineInitSettings { StorageName = "frb2-test-never-created" });

        engine.Storage.IsPersistent.ShouldBeTrue();
        Directory.Exists(FileStorageBackend.DefaultRoot("frb2-test-never-created")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("bad:name")]
    [InlineData(" leading")]
    [InlineData("trailing.")]
    [InlineData("con")]
    public void ConfigureStorage_InvalidName_Throws(string name)
    {
        var engine = new FlatRedBallService();

        Should.Throw<ArgumentException>(() => engine.ConfigureStorage(new EngineInitSettings { StorageName = name }));
    }

    [Theory]
    [InlineData("MyGame")]
    [InlineData("My Game 2")]
    [InlineData("my-game_v1.0")]
    public void ConfigureStorage_ValidName_Succeeds(string name)
    {
        var engine = new FlatRedBallService();

        engine.ConfigureStorage(new EngineInitSettings { StorageName = name, StorageRootDirectory = _root });

        engine.Storage.IsPersistent.ShouldBeTrue();
    }
}
