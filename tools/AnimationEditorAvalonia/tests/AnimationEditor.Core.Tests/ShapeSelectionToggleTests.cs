using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary><see cref="ShapeSelectionToggle.Toggle"/>: Ctrl/Cmd+click membership and order.</summary>
public class ShapeSelectionToggleTests
{
    private static readonly object A = "A", B = "B", C = "C";

    [Fact]
    public void ClickingAnUnselectedShape_AppendsIt() =>
        Assert.Equal(new[] { A, B }, ShapeSelectionToggle.Toggle(new[] { A }, B));

    [Fact]
    public void ClickingASelectedShape_RemovesIt() =>
        Assert.Equal(new[] { A, C }, ShapeSelectionToggle.Toggle(new[] { A, B, C }, B));

    [Fact]
    public void ClickingWithNothingSelected_SelectsIt() =>
        Assert.Equal(new[] { A }, ShapeSelectionToggle.Toggle(new object[0], A));

    [Fact]
    public void ClickingTheOnlySelectedShape_EmptiesTheSelection() =>
        Assert.Empty(ShapeSelectionToggle.Toggle(new[] { A }, A));

    [Fact]
    public void RemovingThePrimary_PromotesTheNextOne() =>
        Assert.Same(B, ShapeSelectionToggle.Toggle(new[] { A, B }, A)[0]);

    [Fact]
    public void DoesNotMutateTheInput()
    {
        var input = new List<object> { A };
        ShapeSelectionToggle.Toggle(input, B);
        Assert.Single(input);
    }
}
