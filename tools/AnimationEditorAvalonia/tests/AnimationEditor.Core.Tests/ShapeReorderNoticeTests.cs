using AnimationEditor.Core.DragDrop;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Dragging shapes to reorder is unsupported (#1285); the toast names the open file's format.</summary>
public class ShapeReorderNoticeTests
{
    [Theory]
    [InlineData(@"C:\anims\hero.achx", ".achx")]
    [InlineData(@"C:\anims\hero.ACHJ", ".achj")]
    [InlineData("/home/u/hero.achj", ".achj")]
    public void Message_NamesTheOpenFilesExtension(string path, string expectedExtension)
    {
        var message = ShapeReorderNotice.Message(path);

        Assert.Contains(expectedExtension, message);
        Assert.Contains("shape", message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\anims\noextension")]
    public void Message_WithoutAnExtension_FallsBackToGenericWording(string? path)
    {
        var message = ShapeReorderNotice.Message(path);

        Assert.Contains(".achx/.achj", message);
    }
}
