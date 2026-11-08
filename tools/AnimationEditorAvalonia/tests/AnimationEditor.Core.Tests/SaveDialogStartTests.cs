using AnimationEditor.Core.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class SaveDialogStartTests
{
    [Fact]
    public void For_AWindowsPath_IsTheFilesFolderAndItsName()
    {
        var start = SaveDialogStart.For(@"C:\projects\game\boss.seqj");

        start.Folder.ShouldNotBeNull().TrimEnd('/').ShouldBe("C:/projects/game");
        start.FileName.ShouldBe("boss.seqj");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void For_NoPath_StartsNowhereInParticular(string? path)
    {
        var start = SaveDialogStart.For(path);

        start.Folder.ShouldBeNull();
        start.FileName.ShouldBeNull();
    }
}
