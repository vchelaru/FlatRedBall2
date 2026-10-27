using FlatRedBall2.Rendering.Batches;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Apos.Shapes' ShapeBatch loads its shader in the constructor, so a game that never draws a shape must not
// construct it. These tests pass a null device because the lazy path never touches it until the first
// shape is drawn.
public class ShapesBatchTests
{
    [Fact]
    public void Initialize_DoesNotCreateShapeBatchUntilFirstUse()
    {
        var batch = new ShapesBatch();

        batch.Initialize(null!);

        batch.IsShapeBatchCreated.ShouldBeFalse();
    }
}
