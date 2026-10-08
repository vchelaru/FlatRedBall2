using System.Linq;
using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

public class WindowsAchxRegistrationTests
{
    [Fact]
    public void BuildWrites_ListsCapabilitiesUnderRegisteredApplications()
    {
        var writes = WindowsAchxRegistration.BuildWrites(@"C:\Users\me\AppData\Local\FlatRedBall2.AnimationEditor\current\AnimationEditor.exe");

        // Windows' Default apps page lists only apps named under RegisteredApplications.
        Assert.Contains(new RegistryValueWrite(
            @"Software\RegisteredApplications", "FlatRedBall AnimationEditor",
            @"Software\FlatRedBall\AnimationEditor\Capabilities"), writes);
        Assert.Contains(new RegistryValueWrite(
            @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".achx",
            "FlatRedBall.AnimationEditor.achx"), writes);
        Assert.Contains(new RegistryValueWrite(
            @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".achj",
            "FlatRedBall.AnimationEditor.achx"), writes);
    }

    [Fact]
    public void BuildWrites_OpenCommand_TargetsGivenExe()
    {
        string exe = @"C:\Users\me\AppData\Local\FlatRedBall2.AnimationEditor\current\AnimationEditor.exe";

        var writes = WindowsAchxRegistration.BuildWrites(exe);

        var openCommand = writes.Single(w => w.SubKey == @"Software\Classes\FlatRedBall.AnimationEditor.achx\shell\open\command");
        Assert.Equal($"\"{exe}\" \"%1\"", openCommand.Value);
    }
}
