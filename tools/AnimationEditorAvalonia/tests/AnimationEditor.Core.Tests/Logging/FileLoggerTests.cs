using System;
using System.IO;
using AnimationEditor.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnimationEditor.Core.Tests.Logging;

public class FileLoggerTests
{
    [Fact]
    public void FormatEntry_WithoutException_WritesTimestampLevelAndMessage()
    {
        var timestamp = new DateTimeOffset(2026, 6, 28, 9, 30, 15, 250, TimeSpan.Zero);

        var line = FileLogger.FormatEntry(timestamp, LogLevel.Information, "App started", null);

        Assert.Equal("[2026-06-28 09:30:15.250] [Information] App started" + Environment.NewLine, line);
    }

    [Fact]
    public void FormatEntry_WithException_IncludesExceptionDetails()
    {
        var timestamp = new DateTimeOffset(2026, 6, 28, 9, 30, 15, 0, TimeSpan.Zero);
        var exception = new InvalidOperationException("boom");

        var line = FileLogger.FormatEntry(timestamp, LogLevel.Error, "Unhandled exception", exception);

        Assert.Contains("[Error] Unhandled exception", line);
        Assert.Contains("InvalidOperationException", line);
        Assert.Contains("boom", line);
    }

    [Fact]
    public void Log_AppendsFormattedEntryToFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-logtest-{Guid.NewGuid():N}.txt");
        try
        {
            ILogger logger = new FileLogger(path);

            logger.LogError(new InvalidOperationException("kaboom"), "Crash in {Source}", "Main");

            var contents = File.ReadAllText(path);
            Assert.Contains("[Error]", contents);
            Assert.Contains("Crash in Main", contents);
            Assert.Contains("kaboom", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Log_AppendsAcrossMultipleCalls()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-logtest-{Guid.NewGuid():N}.txt");
        try
        {
            ILogger logger = new FileLogger(path);

            logger.LogInformation("first");
            logger.LogInformation("second");

            var contents = File.ReadAllText(path);
            Assert.Contains("first", contents);
            Assert.Contains("second", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void FormatEntry_WithContext_PutsContextBetweenLevelAndMessage()
    {
        var timestamp = new DateTimeOffset(2026, 6, 28, 9, 30, 15, 250, TimeSpan.Zero);

        var line = FileLogger.FormatEntry(timestamp, LogLevel.Information, "App started", null, "pid=42 v=1.2.3");

        Assert.Equal("[2026-06-28 09:30:15.250] [Information] [pid=42 v=1.2.3] App started" + Environment.NewLine, line);
    }

    [Fact]
    public void Log_WithContext_PrefixesEntriesWithProcessContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-logtest-{Guid.NewGuid():N}.txt");
        try
        {
            ILogger logger = new FileLogger(path, context: "pid=42 v=1.2.3");

            logger.LogInformation("hello");

            Assert.Contains($"[pid=42 v=1.2.3 t{Environment.CurrentManagedThreadId}]", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // A second launch writes to the same log while the primary has it open; a plain AppendAllText
    // (FileShare.Read) loses those lines to an IOException that the logger then swallows.
    [Fact]
    public void Log_WhileAnotherWriterHoldsTheFileOpen_StillAppends()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-logtest-{Guid.NewGuid():N}.txt");
        try
        {
            using var otherWriter = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            ILogger logger = new FileLogger(path);

            logger.LogInformation("written while shared");

            using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Assert.Contains("written while shared", new StreamReader(reader).ReadToEnd());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Log_WhenThePathCannotBeWritten_DoesNotThrow()
    {
        var blockingFile = Path.Combine(Path.GetTempPath(), $"frb-logtest-{Guid.NewGuid():N}.txt");
        File.WriteAllText(blockingFile, "x");
        try
        {
            // A directory component that is really a file: CreateDirectory and the open both fail.
            ILogger logger = new FileLogger(Path.Combine(blockingFile, "sub", "log.txt"));

            logger.LogError(new InvalidOperationException("boom"), "cannot be written");
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }
}
