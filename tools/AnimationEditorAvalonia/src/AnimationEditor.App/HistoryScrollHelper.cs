namespace AnimationEditor.App.Helpers;

internal static class HistoryScrollHelper
{
    internal static double? ComputeScrollOffset(
        int currentIndex, int totalCount,
        double extent, double viewport, double currentOffsetY)
    {
        if (totalCount == 0 || extent <= viewport) return null;
        double itemHeight = extent / totalCount;
        double itemTop = currentIndex * itemHeight;
        double itemBottom = itemTop + itemHeight;
        if (itemTop < currentOffsetY)
            return itemTop;
        if (itemBottom > currentOffsetY + viewport)
            return itemBottom - viewport;
        return null;
    }
}
