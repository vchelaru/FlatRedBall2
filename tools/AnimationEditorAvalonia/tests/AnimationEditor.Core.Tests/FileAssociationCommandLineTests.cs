using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FileAssociationCommandLineTests
{
    [Fact]
    public void TryParseExePath_QuotedExeAndArg_ReturnsExePath()
    {
        string? exe = FileAssociationCommandLine.TryParseExePath(
            "\"C:\\Program Files\\AnimationEditor\\AnimationEditor.exe\" \"%1\"");

        Assert.Equal(@"C:\Program Files\AnimationEditor\AnimationEditor.exe", exe);
    }

    [Fact]
    public void TryParseExePath_Null_ReturnsNull()
    {
        Assert.Null(FileAssociationCommandLine.TryParseExePath(null));
    }

    [Fact]
    public void TryParseExePath_Unquoted_ReturnsNull()
    {
        Assert.Null(FileAssociationCommandLine.TryParseExePath(@"C:\App.exe %1"));
    }
}
