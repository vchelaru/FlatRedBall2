using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AnimationEditor.Core.Logging;

/// <summary>
/// Minimal <see cref="ILogger"/> that appends formatted entries to a text file. Built for
/// crash logging first, but accepts any level so future warning/info/debug calls flow through
/// the same sink. Write failures are swallowed — logging must never itself crash the app.
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string? _context;
    private static readonly object _gate = new();

    /// <param name="clock">Injected for deterministic timestamps in tests; defaults to local now.</param>
    /// <param name="context">Process identity (pid, version) stamped on every entry, so lines from several processes
    /// appending to one file can be told apart. A <c>t&lt;id&gt;</c> managed-thread tag is added per entry.</param>
    public FileLogger(string filePath, Func<DateTimeOffset>? clock = null, string? context = null)
    {
        _filePath = filePath;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _context = context;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var context = _context == null ? null : $"{_context} t{Environment.CurrentManagedThreadId}";
        Append(FormatEntry(_clock(), logLevel, formatter(state, exception), exception, context));
    }

    /// <summary>
    /// Formats one entry as <c>[yyyy-MM-dd HH:mm:ss.fff] [Level] [context] message</c> (no context bracket when
    /// <paramref name="context"/> is null), followed by the full exception (type, message, inner exceptions, stack)
    /// on the next lines when present.
    /// </summary>
    public static string FormatEntry(DateTimeOffset timestamp, LogLevel level, string message, Exception? exception,
        string? context = null)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] [")
          .Append(level).Append("] ");
        if (context != null)
            sb.Append('[').Append(context).Append("] ");
        sb.Append(message).AppendLine();
        if (exception != null)
            sb.AppendLine(exception.ToString());
        return sb.ToString();
    }

    private void Append(string text)
    {
        try
        {
            lock (_gate)
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                // Opened per entry and closed straight away, so nothing sits in a buffer when the process is
                // killed. ReadWrite sharing lets a second launch append (and the user read) while we have it open.
                using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        catch
        {
            // Logging must never crash the app — swallow IO failures.
        }
    }
}
