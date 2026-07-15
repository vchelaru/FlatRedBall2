using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class OpenFolderLoadFailureTests
{
    [Fact]
    public void FormatMessage_IncludesFolderNameAndDiagnostic()
    {
        var message = OpenFolderLoadFailure.FormatMessage("test", "NotFoundError: A requested file or directory could not be found");

        Assert.Contains("test", message);
        Assert.Contains("NotFoundError", message);
    }

    [Fact]
    public void FormatMessage_NullDiagnostic_FallsBackToUnknown()
    {
        var message = OpenFolderLoadFailure.FormatMessage("test", null);

        Assert.Contains("unknown error", message);
    }

    [Fact]
    public void FormatMessage_MentionsSecuritySoftwareAsPossibleCause()
    {
        var message = OpenFolderLoadFailure.FormatMessage("test", "boom");

        Assert.Contains("antivirus", message, System.StringComparison.OrdinalIgnoreCase);
    }
}
