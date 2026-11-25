using System.IO;

namespace AnimationEditor.App.Services;

/// <summary>The file a launch of the editor asks for.</summary>
internal static class LaunchFile
{
    /// <summary>The existing file <paramref name="arguments"/> name first, or null.</summary>
    public static string? FromArguments(string[] arguments) =>
        arguments.Length >= 1 && File.Exists(arguments[0]) ? arguments[0] : null;
}
