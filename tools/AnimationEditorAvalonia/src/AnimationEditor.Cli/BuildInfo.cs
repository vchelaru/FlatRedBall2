using System.Reflection;

namespace AnimationEditor.Cli;

/// <summary>The one-line <c>--version</c> text, read from attributes the build embedded (no git call, no file scan).</summary>
internal static class BuildInfo
{
    public static string Describe()
    {
        var asm = typeof(BuildInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var plus = info.IndexOf('+');
        if (plus >= 0 && info.Length - plus - 1 > 8) info = info[..(plus + 9)];
        var built = asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value ?? "unknown";
        return $"aecli {info} built {built}";
    }
}
