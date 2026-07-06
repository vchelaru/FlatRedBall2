using System;
using System.Collections.Generic;
using System.Globalization;

namespace AnimationEditor.Core.Git;

/// <summary>
/// Parses the output of <c>git log --follow --name-status</c> (run with <see cref="Format"/>) into
/// <see cref="GitRevision"/>s, newest first. The per-commit path is taken from the name-status line
/// so renames are followed: a rename commit (<c>R100&#9;old&#9;new</c>) yields the file's <c>new</c>
/// path at that commit, so the blob can be fetched with <c>git show &lt;hash&gt;:&lt;pathAtCommit&gt;</c>.
/// </summary>
public static class GitLogParser
{
    // Record/field separators embedded via git's %x1e / %x1f, chosen because they cannot appear in a
    // commit subject or a file path — unlike newlines/tabs, which git also emits structurally.
    private const char RecordSeparator = '\x1e';
    private const char FieldSeparator = '\x1f';

    /// <summary>
    /// The <c>--format</c> argument the git log command must use for <see cref="ParseLog"/> to read
    /// its output: a record separator, then hash / author / ISO-8601 date / subject, field-separated.
    /// </summary>
    public const string Format = "--format=%x1e%H%x1f%an%x1f%aI%x1f%s";

    public static IReadOnlyList<GitRevision> ParseLog(string rawOutput)
    {
        var revisions = new List<GitRevision>();
        if (string.IsNullOrWhiteSpace(rawOutput))
            return revisions;

        // The first split segment is whatever preceded the first record separator (usually empty).
        foreach (var record in rawOutput.Split(RecordSeparator))
        {
            if (string.IsNullOrWhiteSpace(record)) continue;

            var lines = record.Split('\n');
            var fields = lines[0].Split(FieldSeparator);
            if (fields.Length < 4) continue;

            string? pathAtCommit = null;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                if (line.Length == 0) continue;
                // Name-status line: "M\tpath" or "R100\told\tnew" — the file's path in this commit
                // is always the last tab-separated token.
                var tokens = line.Split('\t');
                pathAtCommit = tokens[^1];
                break;
            }

            var date = DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) ? parsed : default;

            revisions.Add(new GitRevision(
                Hash: fields[0].Trim('\r', '\n'),
                Author: fields[1],
                Date: date,
                Subject: fields[3].Trim('\r', '\n'),
                PathAtCommit: pathAtCommit));
        }

        return revisions;
    }
}
