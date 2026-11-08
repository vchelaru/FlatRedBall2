using System;
using System.Globalization;
using System.IO;

namespace AnimationEditor.Core.Logging;

/// <summary>
/// A small file that exists exactly while a primary editor process is running. A launch that finds one left behind
/// knows the earlier run was killed, crashed or hung instead of exiting cleanly, which is otherwise invisible in
/// <c>log.txt</c> (a kill writes nothing). Every operation swallows IO failures.
/// </summary>
public sealed class CleanShutdownMarker
{
    public const string RunningPhase = "running";

    /// <summary>The process is exiting on purpose to install an update; its marker is expected to remain.</summary>
    public const string UpdatingPhase = "updating";

    /// <summary>What a marker left by an earlier run says. Fields are null when the marker was unreadable.</summary>
    public sealed record PreviousRun(int? ProcessId, string? Version, DateTimeOffset? Started, string? Phase)
    {
        /// <summary>True when the earlier run was exiting on purpose to install an update.</summary>
        public bool ExitedForUpdate => Phase == UpdatingPhase;

        public string Describe() =>
            $"pid={ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "?"} v{Version ?? "?"} " +
            $"started {Started?.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "?"} " +
            $"phase={Phase ?? "?"}";
    }

    private readonly string _path;
    private int _processId;
    private string _version = "";
    private DateTimeOffset _started;

    public CleanShutdownMarker(string path) => _path = path;

    /// <summary>
    /// Reads the marker an earlier run left (null when it exited cleanly), then writes this run's marker.
    /// Call it only from the process that owns the single-instance mutex.
    /// </summary>
    public PreviousRun? Begin(int processId, string version, DateTimeOffset now)
    {
        _processId = processId;
        _version = version;
        _started = now;

        PreviousRun? previous = null;
        try
        {
            if (File.Exists(_path))
                previous = Parse(File.ReadAllLines(_path));
        }
        catch
        {
            previous = new PreviousRun(null, null, null, null);
        }

        Write(RunningPhase);
        return previous;
    }

    public void SetPhase(string phase) => Write(phase);

    /// <summary>Removes the marker: this run is exiting normally.</summary>
    public void MarkClean()
    {
        try { File.Delete(_path); }
        catch { /* A marker we cannot delete just reads as an unclean exit next time. */ }
    }

    private void Write(string phase)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllLines(_path,
            [
                $"pid={_processId.ToString(CultureInfo.InvariantCulture)}",
                $"version={_version}",
                $"started={_started.ToString("o", CultureInfo.InvariantCulture)}",
                $"phase={phase}",
            ]);
        }
        catch
        {
            // Diagnostics only: never let the marker affect the app.
        }
    }

    private static PreviousRun Parse(string[] lines)
    {
        int? pid = null;
        string? version = null, phase = null;
        DateTimeOffset? started = null;
        foreach (var line in lines)
        {
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq];
            var value = line[(eq + 1)..];
            switch (key)
            {
                case "pid" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p): pid = p; break;
                case "version": version = value; break;
                case "started" when DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var s): started = s; break;
                case "phase": phase = value; break;
            }
        }
        return new PreviousRun(pid, version, started, phase);
    }
}
