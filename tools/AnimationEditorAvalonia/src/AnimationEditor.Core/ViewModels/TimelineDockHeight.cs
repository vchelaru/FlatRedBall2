namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// Sizing rule for the bottom timeline dock (#1575). Until the user drags the dock's top edge it keeps its
/// automatic height; after a drag, the dragged height is a cap and the dock still collapses to what the
/// current selection needs.
/// </summary>
public static class TimelineDockHeight
{
    /// <summary>The smallest dock height a drag can produce: the ruler plus one frame row.</summary>
    public const double MinimumHeight = 52;

    /// <param name="defaultHeight">The automatic height used until the user drags.</param>
    /// <param name="neededHeight">The height that shows every row of the current selection.</param>
    /// <param name="userHeight">The height set by the last drag, or <c>null</c> if the user never dragged.</param>
    public static double Resolve(double defaultHeight, double neededHeight, double? userHeight) =>
        userHeight is null ? defaultHeight : Math.Min(userHeight.Value, neededHeight);

    /// <summary>Limits a dragged height to <see cref="MinimumHeight"/> and the space the preview offers.</summary>
    public static double ClampDrag(double height, double available) =>
        Math.Max(MinimumHeight, Math.Min(height, available));
}
