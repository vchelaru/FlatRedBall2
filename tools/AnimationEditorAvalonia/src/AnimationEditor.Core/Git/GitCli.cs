using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AnimationEditor.Core.Git;

/// <summary>
/// Thin wrapper over the <c>git</c> command line for the PNG Diff/Blame view: reads a file's revision
/// history, fetches a committed blob's raw bytes, and checks for uncommitted changes. All methods
/// degrade gracefully — a missing <c>git</c> executable or a non-repo path returns a status/empty
/// result rather than throwing. The pure log parsing lives in <see cref="GitLogParser"/>.
/// </summary>
public sealed class GitCli
{
    private readonly string _gitExe;

    /// <param name="gitExe">The git executable name or path; defaults to <c>git</c> on PATH.</param>
    public GitCli(string gitExe = "git") => _gitExe = gitExe;

    /// <summary>
    /// Retrieves the git history for <paramref name="absoluteFilePath"/>, following renames. Returns
    /// a <see cref="GitBlameHistory"/> whose <see cref="GitBlameHistory.Status"/> explains any reason
    /// there is nothing to diff (not a repo, untracked, LFS-stored, or git unavailable).
    /// </summary>
    public GitBlameHistory LoadHistory(string absoluteFilePath)
    {
        string? dir = Path.GetDirectoryName(absoluteFilePath);
        if (string.IsNullOrEmpty(dir))
            return GitBlameHistory.Failed(GitHistoryStatus.NotARepository);

        var top = RunText(dir, "rev-parse", "--show-toplevel");
        if (top.Failed)
            return GitBlameHistory.Failed(GitHistoryStatus.GitUnavailable);
        if (top.ExitCode != 0)
            return GitBlameHistory.Failed(GitHistoryStatus.NotARepository);

        string repoRoot = top.StdOut.Trim();
        string relPath = ToRepoRelative(repoRoot, absoluteFilePath);

        // Tracked? An untracked file has no committed revision to diff against.
        var tracked = RunText(repoRoot, "ls-files", "--error-unmatch", "--", relPath);
        if (tracked.ExitCode != 0)
            return GitBlameHistory.Failed(GitHistoryStatus.Untracked);

        // LFS-stored files commit a pointer, not pixels — pixel diffing is impossible.
        var attr = RunText(repoRoot, "check-attr", "filter", "--", relPath);
        if (attr.StdOut.Contains("filter: lfs"))
            return GitBlameHistory.Failed(GitHistoryStatus.LfsPointer);

        var log = RunText(repoRoot, "log", "--follow", "--name-status", GitLogParser.Format, "--", relPath);
        if (log.Failed || log.ExitCode != 0)
            return GitBlameHistory.Failed(GitHistoryStatus.NotARepository);

        return new GitBlameHistory(GitHistoryStatus.Ok, repoRoot, GitLogParser.ParseLog(log.StdOut));
    }

    /// <summary>
    /// Raw bytes of the file <paramref name="pathAtCommit"/> (repo-relative) at <paramref name="rev"/>
    /// (a commit hash or a ref such as <c>HEAD</c>), or <c>null</c> if the object cannot be read.
    /// </summary>
    public byte[]? GetBlobBytes(string repoRoot, string rev, string pathAtCommit)
    {
        var result = RunBytes(repoRoot, "show", $"{rev}:{pathAtCommit}");
        return result.ExitCode == 0 ? result.StdOut : null;
    }

    /// <summary>True when <paramref name="absoluteFilePath"/> has uncommitted changes (or is staged) relative to HEAD.</summary>
    public bool HasUncommittedChanges(string repoRoot, string absoluteFilePath)
    {
        string relPath = ToRepoRelative(repoRoot, absoluteFilePath);
        var status = RunText(repoRoot, "status", "--porcelain", "--", relPath);
        return status.ExitCode == 0 && !string.IsNullOrWhiteSpace(status.StdOut);
    }

    // git uses forward-slash, repo-root-relative paths for `show <rev>:<path>` and pathspecs.
    private static string ToRepoRelative(string repoRoot, string absoluteFilePath) =>
        Path.GetRelativePath(repoRoot, absoluteFilePath).Replace('\\', '/');

    // ── Process runners ───────────────────────────────────────────────────────

    private readonly record struct TextResult(bool Failed, int ExitCode, string StdOut);
    private readonly record struct BytesResult(bool Failed, int ExitCode, byte[] StdOut);

    private TextResult RunText(string workingDir, params string[] args)
    {
        try
        {
            using var proc = StartGit(workingDir, args, binaryStdout: false);
            var errTask = proc.StandardError.ReadToEndAsync();
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            errTask.Wait();
            return new TextResult(false, proc.ExitCode, stdout);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or FileNotFoundException or PlatformNotSupportedException or InvalidOperationException)
        {
            // git not installed / not on PATH, or process launch unsupported (e.g. WASM).
            return new TextResult(true, -1, "");
        }
    }

    private BytesResult RunBytes(string workingDir, params string[] args)
    {
        try
        {
            using var proc = StartGit(workingDir, args, binaryStdout: true);
            var errTask = proc.StandardError.ReadToEndAsync();
            using var ms = new MemoryStream();
            // Read the raw stdout stream directly so the PNG bytes are never text-decoded.
            proc.StandardOutput.BaseStream.CopyTo(ms);
            proc.WaitForExit();
            errTask.Wait();
            return new BytesResult(false, proc.ExitCode, ms.ToArray());
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or FileNotFoundException or PlatformNotSupportedException or InvalidOperationException)
        {
            return new BytesResult(true, -1, Array.Empty<byte>());
        }
    }

    private Process StartGit(string workingDir, string[] args, bool binaryStdout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _gitExe,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Text output (log, rev-parse) is UTF-8; binary output (blob) reads BaseStream, so its
        // encoding is irrelevant.
        if (!binaryStdout)
            psi.StandardOutputEncoding = Encoding.UTF8;
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        return Process.Start(psi) ?? throw new FileNotFoundException("Failed to start git process.");
    }
}
