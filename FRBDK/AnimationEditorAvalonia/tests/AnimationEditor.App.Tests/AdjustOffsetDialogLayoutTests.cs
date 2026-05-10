using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Layout regression tests for the Adjust Offsets dialog (#121).
/// Ensures the X/Y input row uses a Grid so controls receive proportional
/// width rather than being squashed inside a horizontal StackPanel.
/// </summary>
public class AdjustOffsetDialogLayoutTests
{
    /// <summary>
    /// Verifies that <see cref="MainWindow.BuildAdjustAllRow"/> returns a Grid
    /// whose star columns ensure the NumericUpDown inputs grow with the dialog.
    /// </summary>
    [AvaloniaFact]
    public void BuildAdjustAllRow_UsesGrid_WithStarColumnsForInputs()
    {
        var (row, xInput, yInput) = MainWindow.BuildAdjustAllRow();

        Assert.Equal(4, row.ColumnDefinitions.Count);
        Assert.Equal(GridUnitType.Auto, row.ColumnDefinitions[0].Width.GridUnitType);
        Assert.Equal(GridUnitType.Star, row.ColumnDefinitions[1].Width.GridUnitType);
        Assert.Equal(GridUnitType.Auto, row.ColumnDefinitions[2].Width.GridUnitType);
        Assert.Equal(GridUnitType.Star, row.ColumnDefinitions[3].Width.GridUnitType);

        Assert.Equal(1, Grid.GetColumn(xInput));
        Assert.Equal(3, Grid.GetColumn(yInput));
    }
}
