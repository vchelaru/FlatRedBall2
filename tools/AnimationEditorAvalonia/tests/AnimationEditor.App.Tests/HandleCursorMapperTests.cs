using AnimationEditor.App.Controls;
using AnimationEditor.Core.Rendering;
using Avalonia.Input;
using Xunit;

namespace AnimationEditor.App.Tests;

public class HandleCursorMapperTests
{
    [Theory]
    [InlineData(HandleKind.Move,      StandardCursorType.SizeAll)]
    [InlineData(HandleKind.TopLeft,   StandardCursorType.TopLeftCorner)]
    [InlineData(HandleKind.TopRight,  StandardCursorType.TopRightCorner)]
    [InlineData(HandleKind.BotLeft,   StandardCursorType.BottomLeftCorner)]
    [InlineData(HandleKind.BotRight,  StandardCursorType.BottomRightCorner)]
    [InlineData(HandleKind.MidLeft,   StandardCursorType.SizeWestEast)]
    [InlineData(HandleKind.MidRight,  StandardCursorType.SizeWestEast)]
    [InlineData(HandleKind.TopCenter, StandardCursorType.SizeNorthSouth)]
    [InlineData(HandleKind.BotCenter, StandardCursorType.SizeNorthSouth)]
    public void Maps_each_handle_kind_to_the_expected_cursor(HandleKind kind, StandardCursorType expected)
    {
        Assert.Equal(expected, HandleCursorMapper.CursorTypeFor(kind));
    }

    [Fact]
    public void None_returns_null_meaning_platform_default()
    {
        Assert.Null(HandleCursorMapper.CursorTypeFor(HandleKind.None));
    }
}
