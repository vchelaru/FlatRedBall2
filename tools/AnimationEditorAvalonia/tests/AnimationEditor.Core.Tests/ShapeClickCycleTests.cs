using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// <see cref="ShapeClickCycle.NextTarget"/>: which shape a click selects when several overlap
/// (stack is topmost-first). Clicking the already-selected shape moves to the next one under it.
/// </summary>
public class ShapeClickCycleTests
{
    private static readonly object A = "A", B = "B", C = "C";

    [Fact]
    public void EmptyStack_ReturnsNull() =>
        Assert.Null(ShapeClickCycle.NextTarget(new object[0], null));

    [Fact]
    public void NothingSelected_ReturnsTopmost() =>
        Assert.Same(A, ShapeClickCycle.NextTarget(new[] { A, B }, null));

    [Fact]
    public void SelectedNotInStack_ReturnsTopmost() =>
        Assert.Same(A, ShapeClickCycle.NextTarget(new[] { A, B }, C));

    [Fact]
    public void SelectedInStack_ReturnsNextBelow() =>
        Assert.Same(B, ShapeClickCycle.NextTarget(new[] { A, B, C }, A));

    [Fact]
    public void SelectedIsBottom_WrapsToTopmost() =>
        Assert.Same(A, ShapeClickCycle.NextTarget(new[] { A, B, C }, C));

    [Fact]
    public void SelectedIsOnlyShape_ReturnsItself() =>
        Assert.Same(A, ShapeClickCycle.NextTarget(new[] { A }, A));
}
