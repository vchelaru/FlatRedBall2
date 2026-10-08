using System;

namespace AnimationEditor.Core.IO;

/// <summary>
/// A file's size and last-modified time, cheap enough to read without opening the file. Used to
/// key cached data (e.g. <see cref="AchxThumbnailCacheKey"/>) to a specific version of a file.
/// </summary>
public readonly record struct FolderEntrySnapshot(ulong? Size, DateTimeOffset? Modified);
