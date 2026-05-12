using System;
using System.Collections.Generic;
using System.IO;

namespace AnimationEditor.Core.Paths;

/// <summary>
/// Wraps a path string with case-insensitive equality, slash normalization, and ../ collapsing.
/// Lifted from FlatRedBall 1's <c>FlatRedBall.IO.FilePath</c> with all <c>FileManager</c> and other
/// global dependencies inlined as private static helpers so it stands alone in
/// <see cref="AnimationEditor.Core"/>.
/// </summary>
/// <remarks>
/// Relative paths are resolved against <see cref="Environment.CurrentDirectory"/>. <see cref="Standardized"/>
/// is lowercased for comparison; <see cref="StandardizedCaseSensitive"/> and <see cref="FullPath"/>
/// preserve original case (AE depends on case preservation when writing texture names back into .achx).
/// </remarks>
public class FilePath : IComparable, IEquatable<FilePath>
{
    public bool Equals(FilePath? other) => other != null && Standardized == other.Standardized;

    public string? Original { get; }

    public static bool operator ==(FilePath? f1, FilePath? f2) => f1?.Standardized == f2?.Standardized;
    public static bool operator !=(FilePath? f1, FilePath? f2) => !(f1 == f2);

    public static implicit operator FilePath?(string? s) => s == null ? null : new FilePath(s);

    private string? _extensionCache;
    /// <summary>Lower-case extension without a period (e.g. "png"). Empty if no extension.</summary>
    public string Extension => _extensionCache ??= GetExtension(Original);

    public string StandardizedNoPathNoExtension => RemovePath(RemoveExtension(Standardized));
    public string NoPathNoExtension => RemovePath(RemoveExtension(FullPath));
    public string NoPath => RemovePath(FullPath);

    private string? _fullPathCache;
    /// <summary>Forward-slashed, ../-collapsed, current-dir-prepended-if-relative. Case preserved.</summary>
    public string FullPath => _fullPathCache ??= RemoveDotDotSlash(StandardizeInternal(Original ?? ""));

    private string? _standardizedCache;
    /// <summary>Same as <see cref="FullPath"/> but lower-cased for case-insensitive comparison.</summary>
    public string Standardized => _standardizedCache ??= FullPath.ToLowerInvariant();

    private string? _standardizedCaseSensitive;
    /// <summary>Forward-slashed, ../-collapsed, case-preserved. Identical to <see cref="FullPath"/> for non-null Original.</summary>
    public string? StandardizedCaseSensitive
    {
        get
        {
            if (_standardizedCaseSensitive == null && Original != null)
            {
                _standardizedCaseSensitive = RemoveDotDotSlash(StandardizeInternal(Original));
            }
            return _standardizedCaseSensitive;
        }
    }

    public bool IsDirectory => string.IsNullOrEmpty(Extension);

    /// <param name="path">Absolute or relative path. If relative, resolved against the current directory at the time properties are first accessed.</param>
    public FilePath(string path)
    {
        Original = path;
    }

    public override bool Equals(object? obj)
    {
        if (obj is FilePath path) return Standardized == path.Standardized;
        if (obj is string s) return Standardized == new FilePath(s).Standardized;
        return false;
    }

    public FilePath GetDirectoryContainingThis() => GetDirectoryStandardized(StandardizedCaseSensitive ?? "");

    public override int GetHashCode()
    {
        unchecked
        {
            return 354063820 * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Standardized);
        }
    }

    public bool Exists()
    {
        try
        {
            var standardized = StandardizedCaseSensitive;
            if (string.IsNullOrEmpty(standardized)) return false;
            if (standardized.EndsWith("/")) return Directory.Exists(standardized);
            return File.Exists(standardized) || Directory.Exists(standardized);
        }
        catch (InvalidOperationException)
        {
            // Too many ../'s walked past the root.
            return false;
        }
    }

    public bool IsRootOf(FilePath otherFilePath)
        => otherFilePath.Standardized.StartsWith(Standardized) && otherFilePath != this;

    public FilePath RemoveExtension() => new FilePath(RemoveExtension(Original ?? ""));

    public override string ToString() => StandardizedCaseSensitive ?? "";

    public int CompareTo(object? obj)
    {
        if (obj is FilePath other) return FullPath.CompareTo(other.FullPath);
        if (obj is string s) return FullPath.CompareTo(s);
        return 0;
    }

    public bool IsRelativeTo(FilePath otherFilePath) => IsRelativeTo(FullPath, otherFilePath.FullPath);

    public string RelativeTo(FilePath otherFilePath) => MakeRelative(FullPath, otherFilePath.FullPath);

    // ---------------------------------------------------------------------
    // Inlined pure helpers, ported from FRB1 FileManager. Behavior preserved
    // verbatim for the cases AE exercises; FRB1-only branches (URL, FTP,
    // isolated storage, USES_DOT_SLASH_ABSOLUTE_FILES, RelativeDirectory
    // global) are dropped.
    // ---------------------------------------------------------------------

    private static void ReplaceSlashes(ref string s)
    {
        bool isNetwork = s.StartsWith(@"\\");
        if (isNetwork) s = s.Substring(2);
        s = s.Replace('\\', '/');
        if (isNetwork) s = @"\\" + s;
    }

    private static string StandardizeInternal(string fileNameToFix)
    {
        bool isNetwork = fileNameToFix.StartsWith(@"\\");
        ReplaceSlashes(ref fileNameToFix);

        if (!isNetwork && IsRelative(fileNameToFix))
        {
            var relativeDirectory = (Environment.CurrentDirectory + "/").Replace('\\', '/');
            fileNameToFix = relativeDirectory + fileNameToFix;
            ReplaceSlashes(ref fileNameToFix);
        }

        var beforeFix = fileNameToFix;
        fileNameToFix = RemoveDotDotSlash(fileNameToFix);

        if (fileNameToFix.StartsWith(".."))
        {
            throw new InvalidOperationException($"Tried to remove all ../ from {beforeFix} but ended up with this: " + fileNameToFix);
        }

        return fileNameToFix.Replace("//", "/");
    }

    private static bool IsRelative(string fileName)
    {
        if (fileName == null) throw new ArgumentException("Cannot check if a null file name is relative.");
        // Windows drive prefix ("C:..."): Path.IsPathRooted returns false on Linux for these,
        // which would silently turn a Windows-authored absolute path into a relative one. AE
        // files may originate cross-platform, so recognize the drive form regardless of host.
        if (fileName.Length >= 2 && char.IsLetter(fileName[0]) && fileName[1] == ':') return false;
        try { return !Path.IsPathRooted(fileName); }
        catch (ArgumentException e) { throw new ArgumentException($"Argument exception on {fileName}", e); }
    }

    private static string GetExtension(string? fileName)
    {
        if (fileName == null) return "";
        int i = fileName.LastIndexOf('.');
        if (i == -1) return "";

        bool hasDotSlash = i < fileName.Length - 1 && (fileName[i + 1] == '/' || fileName[i + 1] == '\\');
        bool hasSlashAfterDot = i < fileName.LastIndexOf('/') || i < fileName.LastIndexOf('\\');
        if (hasDotSlash || hasSlashAfterDot) return "";
        return fileName.Substring(i + 1).ToLowerInvariant();
    }

    private static string RemoveExtension(string fileName)
    {
        int extLen = GetExtension(fileName).Length;
        if (extLen == 0) return fileName;
        if (fileName.Length > extLen && fileName[fileName.Length - (extLen + 1)] == '.')
            return fileName.Substring(0, fileName.Length - (extLen + 1));
        return fileName;
    }

    private static string RemovePath(string fileName)
    {
        int i1 = fileName.LastIndexOf('/');
        if (i1 == fileName.Length - 1 && fileName.Length > 1) i1 = fileName.LastIndexOf('/', fileName.Length - 2);
        int i2 = fileName.LastIndexOf('\\');
        if (i2 == fileName.Length - 1 && fileName.Length > 1) i2 = fileName.LastIndexOf('\\', fileName.Length - 2);
        if (i1 > i2) return fileName.Remove(0, i1 + 1);
        if (i2 != -1) return fileName.Remove(0, i2 + 1);
        return fileName;
    }

    private static string RemoveDotDotSlash(string s)
    {
        if (s.Contains(".."))
        {
            s = s.Replace('\\', '/');
            int idx = GetDotDotSlashIndex(s);
            while (idx > 0)
            {
                idx++; // shift "/../" -> "../"
                int prev = s.LastIndexOf('/', idx - 2, idx - 2);
                s = s.Remove(prev + 1, idx - prev + 2);
                idx = GetDotDotSlashIndex(s);
            }
        }
        s = s.Replace("/./", "/").Replace(@"\.\", @"\").Replace(@"/.\", "/").Replace(@"\./", @"\");
        return s;
    }

    private static int GetDotDotSlashIndex(string s)
    {
        int idx = s.LastIndexOf("/../");
        while (idx > 0 && s[idx - 1] == '.') idx = s.LastIndexOf("/../", idx);
        return idx;
    }

    private static FilePath GetDirectoryStandardized(string fileName)
    {
        int last = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
        if (last == fileName.Length - 1)
        {
            last = Math.Max(
                fileName.LastIndexOf('/', fileName.Length - 2),
                fileName.LastIndexOf('\\', fileName.Length - 2));
        }
        if (last == -1) return new FilePath("");
        return new FilePath(fileName.Substring(0, last + 1).Replace('\\', '/'));
    }

    private static bool IsRelativeTo(string fileName, string directory)
    {
        if (string.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));
        if (string.IsNullOrEmpty(directory)) throw new ArgumentNullException(nameof(directory));

        if (!IsRelative(fileName) && !IsRelative(directory))
        {
            fileName = fileName.Replace('\\', '/');
            directory = directory.Replace('\\', '/');
            if (!directory.EndsWith("/")) directory += "/";
            return fileName.IndexOf(directory, StringComparison.OrdinalIgnoreCase) == 0;
        }
        return false;
    }

    private static string MakeRelative(string path, string relativeTo)
    {
        if (string.IsNullOrEmpty(path)) return path;
        path = path.Replace('\\', '/');
        relativeTo = relativeTo.Replace('\\', '/');
        if (!relativeTo.EndsWith("/")) relativeTo += "/";

        if (path.StartsWith(relativeTo, StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(relativeTo.Length);
        }

        var pathParts = path.Split('/');
        var relParts = relativeTo.Split('/');
        int start = 0;
        while (start < pathParts.Length && start < relParts.Length &&
               string.Equals(pathParts[start], relParts[start], StringComparison.OrdinalIgnoreCase))
        {
            start++;
        }
        if (start == 0) return path;

        var result = "";
        for (int i = start; i < relParts.Length; i++)
            if (relParts[i] != string.Empty) result += "../";
        if (result == "" && pathParts.Length - start > 0) result += "./";
        for (int i = start; i < pathParts.Length; i++)
        {
            result += pathParts[i];
            if (i < pathParts.Length - 1) result += "/";
        }
        return result;
    }
}
