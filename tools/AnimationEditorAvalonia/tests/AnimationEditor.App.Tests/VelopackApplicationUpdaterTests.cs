using AnimationEditor.App.Services;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

public class VelopackApplicationUpdaterTests
{
    // The test process never runs VelopackApp.Build().Run(), so the update manager cannot be created.
    [Fact]
    public async Task DownloadUpdateAsync_WhenTheUpdateManagerCannotBeCreated_ReturnsFailed()
    {
        ApplicationUpdateResult result = await new VelopackApplicationUpdater().DownloadUpdateAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ApplicationUpdateStatus.Failed);
    }
}
