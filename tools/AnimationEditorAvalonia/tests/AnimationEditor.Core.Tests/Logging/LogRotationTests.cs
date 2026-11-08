using System;
using System.IO;
using AnimationEditor.Core.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class LogRotationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"frb-logrot-{Guid.NewGuid():N}");

    public LogRotationTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RotateIfLarge_OverLimit_MovesLogToOldFile()
    {
        var log = Path.Combine(_dir, "log.txt");
        File.WriteAllText(log, new string('a', 100));

        var rotated = LogRotation.RotateIfLarge(log, maxBytes: 50);

        Assert.True(rotated);
        Assert.False(File.Exists(log));
        Assert.Equal(new string('a', 100), File.ReadAllText(Path.Combine(_dir, "log.old.txt")));
    }

    [Fact]
    public void RotateIfLarge_OverLimit_ReplacesAnEarlierOldFile()
    {
        var log = Path.Combine(_dir, "log.txt");
        File.WriteAllText(Path.Combine(_dir, "log.old.txt"), "stale");
        File.WriteAllText(log, new string('b', 100));

        LogRotation.RotateIfLarge(log, maxBytes: 50);

        Assert.Equal(new string('b', 100), File.ReadAllText(Path.Combine(_dir, "log.old.txt")));
    }

    [Fact]
    public void RotateIfLarge_UnderLimit_LeavesLogAlone()
    {
        var log = Path.Combine(_dir, "log.txt");
        File.WriteAllText(log, "small");

        Assert.False(LogRotation.RotateIfLarge(log, maxBytes: 50));
        Assert.Equal("small", File.ReadAllText(log));
    }

    [Fact]
    public void RotateIfLarge_MissingFile_DoesNothing()
    {
        Assert.False(LogRotation.RotateIfLarge(Path.Combine(_dir, "nope.txt"), maxBytes: 50));
    }

    [Fact]
    public void RotateIfLarge_WhenTheFileIsLocked_DoesNotThrow()
    {
        var log = Path.Combine(_dir, "log.txt");
        File.WriteAllText(log, new string('c', 100));
        using var locked = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.None);

        // Windows refuses the move; Linux/macOS only advise on FileShare.None and move it anyway. Either way: no throw.
        Assert.Null(Record.Exception(() => LogRotation.RotateIfLarge(log, maxBytes: 50)));
    }
}
