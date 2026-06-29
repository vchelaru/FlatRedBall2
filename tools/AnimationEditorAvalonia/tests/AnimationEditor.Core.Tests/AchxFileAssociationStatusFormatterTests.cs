using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxFileAssociationStatusFormatterTests
{
    [Fact]
    public void Describe_Stale_MentionsPreviousInstall()
    {
        string text = AchxFileAssociationStatusFormatter.Describe(AchxFileAssociationStatus.Stale);

        Assert.Contains("previous", text, StringComparison.OrdinalIgnoreCase);
    }
}
