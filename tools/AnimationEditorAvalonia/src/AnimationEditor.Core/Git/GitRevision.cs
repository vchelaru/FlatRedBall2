using System;

namespace AnimationEditor.Core.Git;

/// <summary>
/// One entry in a PNG's git history, as shown in the Diff revision list. Also used for the
/// synthetic "working tree" entry (uncommitted on-disk state) via <see cref="WorkingTree"/>.
/// </summary>
public sealed record GitRevision(
    string Hash, string Author, DateTimeOffset Date, string Subject, string? PathAtCommit)
{
    /// <summary>The abbreviated commit hash (first 7 chars) for display; empty for the working-tree entry.</summary>
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;

    /// <summary>True for the synthetic uncommitted-changes entry, which is diffed against HEAD rather than a parent commit.</summary>
    public bool IsWorkingTree => Hash.Length == 0;

    /// <summary>
    /// The synthetic top-of-list entry representing the file's current on-disk state (uncommitted
    /// changes), diffed against HEAD. <paramref name="pathAtHead"/> is the file's path at HEAD, used
    /// to fetch the "before" blob.
    /// </summary>
    public static GitRevision WorkingTree(string pathAtHead) =>
        new(Hash: "", Author: "", Date: default, Subject: "Working tree (uncommitted)", pathAtHead);
}
