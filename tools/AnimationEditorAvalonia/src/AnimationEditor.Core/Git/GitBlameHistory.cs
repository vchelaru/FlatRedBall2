using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Git;

/// <summary>Why a PNG has no usable git history, or that it does (<see cref="Ok"/>).</summary>
public enum GitHistoryStatus
{
    /// <summary>History was retrieved successfully (may still be empty if the file is brand-new).</summary>
    Ok,

    /// <summary>The file is not inside a git working tree.</summary>
    NotARepository,

    /// <summary>The file is inside a repo but has never been committed — no prior revision to diff against.</summary>
    Untracked,

    /// <summary>The file is stored via Git LFS; committed blobs are pointer files, not image data, so pixel diffing is not possible.</summary>
    LfsPointer,

    /// <summary>The <c>git</c> executable could not be run (not installed / not on PATH).</summary>
    GitUnavailable,
}

/// <summary>
/// The result of asking git for a PNG's revision history: a status plus, when
/// <see cref="GitHistoryStatus.Ok"/>, the repository root and the commits that touched the file
/// (newest first, renames followed).
/// </summary>
public sealed record GitBlameHistory(
    GitHistoryStatus Status, string RepositoryRoot, IReadOnlyList<GitRevision> Revisions)
{
    public static GitBlameHistory Failed(GitHistoryStatus status) =>
        new(status, "", Array.Empty<GitRevision>());
}
