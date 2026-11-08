using System;
using AnimationEditor.Core.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class SessionHeaderTests
{
    [Fact]
    public void Format_NamesVersionOsArgsExecutableAndLogPath()
    {
        var text = SessionHeader.Format(
            version: "2026.11.8.3",
            os: "Microsoft Windows 10.0.26200",
            args: ["C:\\art\\hero.achx", "--flag"],
            exePath: "C:\\Apps\\AnimationEditor\\current\\AnimationEditor.exe",
            logPath: "C:\\Users\\dev\\AppData\\Roaming\\AnimationEditor\\log.txt");

        Assert.Contains("Process start", text);
        Assert.Contains("version=2026.11.8.3", text);
        Assert.Contains("os=Microsoft Windows 10.0.26200", text);
        Assert.Contains("args=[C:\\art\\hero.achx | --flag]", text);
        Assert.Contains("exe=C:\\Apps\\AnimationEditor\\current\\AnimationEditor.exe", text);
        Assert.Contains("log=C:\\Users\\dev\\AppData\\Roaming\\AnimationEditor\\log.txt", text);
    }

    [Fact]
    public void Format_WithNoArgsOrExe_StillWritesTheLine()
    {
        var text = SessionHeader.Format("1.0", "os", [], exePath: null, logPath: "log.txt");

        Assert.Contains("args=[]", text);
        Assert.Contains("exe=(unknown)", text);
    }

    [Fact]
    public void Context_CarriesPidAndVersion()
    {
        Assert.Equal("pid=42 v1.2.3", SessionHeader.Context(42, "1.2.3"));
    }

    [Fact]
    public void ExitedForUpdate_IsTrueOnlyForTheUpdatingPhase()
    {
        var started = DateTimeOffset.UnixEpoch;

        Assert.True(new CleanShutdownMarker.PreviousRun(1, "1.0", started, CleanShutdownMarker.UpdatingPhase).ExitedForUpdate);
        Assert.False(new CleanShutdownMarker.PreviousRun(1, "1.0", started, CleanShutdownMarker.RunningPhase).ExitedForUpdate);
        Assert.False(new CleanShutdownMarker.PreviousRun(null, null, null, null).ExitedForUpdate);
    }
}
