using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

/// <summary>
/// Runs <see cref="UnloadableContentManager"/> against KNI's real XNB reader. Assets are tiny
/// in-memory XNBs read by <see cref="FakeAssetReader"/>, so external references go through KNI's
/// own <c>ContentReader.ReadExternalReference</c> path, with no GraphicsDevice or disk.
/// </summary>
public class UnloadableContentManagerTests
{
    [Fact]
    public void ContentLoaderUnload_LoadedAsset_DisposesItAndReloadReturnsNewInstance()
    {
        var manager = new InMemoryContentManager();
        manager.AddAsset("ship");
        var loader = new ContentLoader();
        loader.Initialize(manager, null!);
        var first = loader.Load<FakeAsset>("ship");

        loader.Unload("ship");
        var second = loader.Load<FakeAsset>("ship");

        first.IsDisposed.ShouldBeTrue();
        second.ShouldNotBeSameAs(first);
        second.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public void UnloadAsset_NotLoaded_DoesNothing()
    {
        var manager = new InMemoryContentManager();
        manager.AddAsset("ship");
        var ship = manager.Load<FakeAsset>("ship");

        manager.UnloadAsset("never-loaded");

        ship.IsDisposed.ShouldBeFalse();
        manager.Load<FakeAsset>("ship").ShouldBeSameAs(ship);
    }

    [Fact]
    public void UnloadAsset_DifferentSlashesAndCase_UnloadsSameAsset()
    {
        var manager = new InMemoryContentManager();
        manager.AddAsset("ships/red");
        var ship = manager.Load<FakeAsset>("ships/red");

        manager.UnloadAsset("SHIPS\\red");

        ship.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void UnloadAsset_ParentWithSharedExternalReference_LeavesChildUsable()
    {
        // Both parents reference "shared" as an external reference; KNI loads it through Load<T>,
        // so it gets its own cache entry and is not owned by either parent.
        var manager = new InMemoryContentManager();
        manager.AddAsset("shared");
        manager.AddAsset("parentA", externalReference: "shared");
        manager.AddAsset("parentB", externalReference: "shared");
        var parentA = manager.Load<FakeAsset>("parentA");
        var parentB = manager.Load<FakeAsset>("parentB");

        manager.UnloadAsset("parentA");

        parentA.IsDisposed.ShouldBeTrue();
        parentA.Child.ShouldBeSameAs(parentB.Child);
        parentB.Child!.IsDisposed.ShouldBeFalse();
        manager.Load<FakeAsset>("shared").ShouldBeSameAs(parentB.Child);
    }

    [Fact]
    public void Unload_LoadedAssets_DisposesAllAndClearsCache()
    {
        var manager = new InMemoryContentManager();
        manager.AddAsset("shared");
        manager.AddAsset("parent", externalReference: "shared");
        var parent = manager.Load<FakeAsset>("parent");

        manager.Unload();

        parent.IsDisposed.ShouldBeTrue();
        parent.Child!.IsDisposed.ShouldBeTrue();
        manager.Load<FakeAsset>("parent").ShouldNotBeSameAs(parent);
    }

    private sealed class InMemoryContentManager : UnloadableContentManager
    {
        private readonly Dictionary<string, byte[]> _assets = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryContentManager() : base(new GameServiceContainer(), "") { }

        public void AddAsset(string name, string? externalReference = null)
            => _assets[name] = BuildXnb(name, externalReference);

        protected override Stream OpenStream(string assetName)
            => new MemoryStream(_assets[assetName.Replace('\\', '/')]);

        // Uncompressed XNB v5: header, one type reader, no shared resources, then the object.
        private static byte[] BuildXnb(string name, string? externalReference)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("XNB"));
                writer.Write((byte)'d');
                writer.Write((byte)5);
                writer.Write((byte)0);
                writer.Write(0u);
                writer.Write7BitEncodedInt(1);
                writer.Write(typeof(FakeAssetReader).AssemblyQualifiedName!);
                writer.Write(0);
                writer.Write7BitEncodedInt(0);
                writer.Write7BitEncodedInt(1);
                writer.Write(name);
                writer.Write(externalReference ?? "");
            }
            return stream.ToArray();
        }
    }
}

public sealed class FakeAsset : IDisposable
{
    public string Name { get; init; } = "";
    public FakeAsset? Child { get; init; }
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public sealed class FakeAssetReader : ContentTypeReader<FakeAsset>
{
    protected override FakeAsset Read(ContentReader input, FakeAsset existingInstance)
    {
        var name = input.ReadString();
        // ReadExternalReference reads its own string and returns default for an empty one.
        var child = input.ReadExternalReference<FakeAsset>();
        return new FakeAsset { Name = name, Child = child };
    }
}
