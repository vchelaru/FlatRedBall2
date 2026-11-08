using System;
using System.IO;
using AnimationEditor.App.Services;
using AnimationEditor.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies the handler-to-logger wiring (issue #480): invoking the crash-log delegate
/// with a synthetic exception produces a log line. The actual event subscriptions and
/// <c>Process.Start</c> remain the thin untested boundary.
/// </summary>
public class CrashLoggingTests
{
    [Fact]
    public void Log_WithInnerExceptionAndTerminatingFlag_RecordsBoth()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-crashlog-{Guid.NewGuid():N}.txt");
        try
        {
            ILogger logger = new FileLogger(path);
            var exception = new InvalidOperationException("outer", new IOException("inner-root-cause"));

            CrashLogging.Log(logger, "AppDomain.UnhandledException", exception, terminating: true);

            var contents = File.ReadAllText(path);
            Assert.Contains("terminating=True", contents);
            Assert.Contains("inner-root-cause", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CloseCleanup_WhenAStepFails_StillRunsTheRestAndReportsTheFailure()
    {
        var ran = new System.Collections.Generic.List<string>();
        var errors = new System.Collections.Generic.List<Exception>();

        CloseCleanup.Run(
            [() => ran.Add("a"), () => throw new InvalidOperationException("step-boom"), () => ran.Add("c")],
            errors.Add);

        Assert.Equal(new[] { "a", "c" }, ran);
        Assert.Equal("step-boom", Assert.Single(errors).Message);
    }

    [Fact]
    public void Log_WithException_WritesSourceAndExceptionToSink()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frb-crashlog-{Guid.NewGuid():N}.txt");
        try
        {
            ILogger logger = new FileLogger(path);

            CrashLogging.Log(logger, "Dispatcher.UnhandledException", new InvalidOperationException("ui-thread-boom"));

            var contents = File.ReadAllText(path);
            Assert.Contains("Dispatcher.UnhandledException", contents);
            Assert.Contains("ui-thread-boom", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
