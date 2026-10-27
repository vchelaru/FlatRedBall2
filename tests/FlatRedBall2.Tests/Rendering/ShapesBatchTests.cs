using System.Runtime.CompilerServices;
using Apos.Shapes;
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

    [Fact]
    public void Shapes_AccessedTwice_CreatesShapeBatchOnceAndReturnsSameInstance()
    {
        var batch = new ShapesBatch();
        var createCount = 0;
        // ShapeBatch's constructor needs a real device; an uninitialized instance is enough to check identity.
        batch.CreateShapeBatch = _ =>
        {
            createCount++;
            return (ShapeBatch)RuntimeHelpers.GetUninitializedObject(typeof(ShapeBatch));
        };
        batch.Initialize(null!);

        var first = batch.Shapes;
        var second = batch.Shapes;

        createCount.ShouldBe(1);
        second.ShouldBeSameAs(first);
        batch.IsShapeBatchCreated.ShouldBeTrue();
    }
}
