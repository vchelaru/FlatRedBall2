using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests the pure, platform-agnostic helpers on <see cref="WindowsFileAssociationService"/>
/// (the shell-open-command string and ProgId comparison). The registry I/O and the
/// open-settings deep-link are thin, untested wiring around these.
/// </summary>
public class WindowsFileAssociationTests
{
    [Fact]
    public void BuildOpenCommand_QuotesExeAndArgPlaceholder()
    {
        string command = WindowsFileAssociationService.BuildOpenCommand(
            @"C:\Program Files\AnimationEditor\AnimationEditor.exe");

        Assert.Equal(
            "\"C:\\Program Files\\AnimationEditor\\AnimationEditor.exe\" \"%1\"",
            command);
    }

    [Fact]
    public void IsOurProgId_DifferentCase_ReturnsTrue()
    {
        bool result = WindowsFileAssociationService.IsOurProgId(
            WindowsFileAssociationService.ProgId.ToUpperInvariant());

        Assert.True(result);
    }

    [Fact]
    public void IsOurProgId_ForeignProgId_ReturnsFalse()
    {
        bool result = WindowsFileAssociationService.IsOurProgId("SomeOther.App.achx");

        Assert.False(result);
    }

    [Fact]
    public void IsOurProgId_Null_ReturnsFalse()
    {
        bool result = WindowsFileAssociationService.IsOurProgId(null);

        Assert.False(result);
    }
}
