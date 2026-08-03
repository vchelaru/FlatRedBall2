namespace RiftboundSample.Systems;

/// <summary>
/// Resolves relative data file paths against the executable's directory,
/// ensuring files are found regardless of the working directory.
/// </summary>
public static class DataPath
{
    private static readonly string BaseDir = AppContext.BaseDirectory;

    public static string Resolve(string relativePath) => Path.Combine(BaseDir, relativePath);
}
