using System.Linq;
using System.Reflection;

namespace AnimationEditor.App.Services;

internal static class AppVersion
{
    /// <summary>The build's version (e.g. <c>2026.11.8.3</c>), without any source-control suffix.</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
            return informational.Split('+').First();
        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
