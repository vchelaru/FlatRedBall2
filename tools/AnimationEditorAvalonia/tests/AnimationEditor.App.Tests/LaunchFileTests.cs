using AnimationEditor.App.Services;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

public class LaunchFileTests
{
    [Fact]
    public void FromArguments_ARelativePath_IsTheFullPath()
    {
        // The test host's folder holds some file to name.
        string file = new DirectoryInfo(Environment.CurrentDirectory).EnumerateFiles().First().Name;

        LaunchFile.FromArguments([file]).ShouldBe(Path.Combine(Environment.CurrentDirectory, file));
    }
}
