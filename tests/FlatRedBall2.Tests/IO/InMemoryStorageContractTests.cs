using FlatRedBall2.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.IO;

public class InMemoryStorageContractTests : StorageBackendContractTests
{
    protected override IStorageBackend CreateBackend() => new InMemoryStorageBackend();

    [Fact]
    public void NewEngine_DefaultStorage_IsInMemoryAndNotPersistent()
    {
        var storage = new FlatRedBallService().Storage;

        storage.Backend.ShouldBeOfType<InMemoryStorageBackend>();
        storage.IsPersistent.ShouldBeFalse();
    }
}
