using AnimationEditor.Core.Rendering;
using Avalonia.Input;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Pure mapping from a hit-tested <see cref="HandleKind"/> to the Avalonia
/// <see cref="StandardCursorType"/> the wireframe should display while
/// hovering. Returns <c>null</c> when the platform default cursor should be used.
/// </summary>
public static class HandleCursorMapper
{
    public static StandardCursorType? CursorTypeFor(HandleKind kind) => kind switch
    {
        HandleKind.Move      => StandardCursorType.SizeAll,
        HandleKind.TopLeft   => StandardCursorType.TopLeftCorner,
        HandleKind.BotRight  => StandardCursorType.BottomRightCorner,
        HandleKind.TopRight  => StandardCursorType.TopRightCorner,
        HandleKind.BotLeft   => StandardCursorType.BottomLeftCorner,
        HandleKind.MidLeft or
        HandleKind.MidRight  => StandardCursorType.SizeWestEast,
        HandleKind.TopCenter or
        HandleKind.BotCenter => StandardCursorType.SizeNorthSouth,
        _                    => null,
    };
}
