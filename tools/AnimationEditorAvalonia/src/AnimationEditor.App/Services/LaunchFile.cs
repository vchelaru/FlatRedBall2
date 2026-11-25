using System.IO;

namespace AnimationEditor.App.Services;

/// <summary>The file a launch of the editor asks for.</summary>
internal static class LaunchFile
{
    /// <summary>
    /// The full path of the existing file <paramref name="arguments"/> name first, or null. A running editor the path is
    /// handed to has another current folder.
    /// </summary>
    public static string? FromArguments(string[] arguments) =>
        arguments.Length >= 1 && File.Exists(arguments[0]) ? Path.GetFullPath(arguments[0]) : null;
}
