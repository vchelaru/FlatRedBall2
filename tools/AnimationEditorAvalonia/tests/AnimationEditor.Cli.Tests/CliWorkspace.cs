using AnimationEditor.Cli;

namespace AnimationEditor.Cli.Tests;

/// <summary>A temp folder holding copies of the checked-in fixtures, plus a runner for CLI commands.</summary>
public sealed class CliWorkspace : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "AeCliTests", Guid.NewGuid().ToString("N"));

    public CliWorkspace(params string[] fixtures)
    {
        Directory.CreateDirectory(Dir);
        foreach (var f in fixtures) Copy(f);
    }

    public string PathOf(string name) => Path.Combine(Dir, name);

    public string Copy(string fixture)
    {
        var dest = PathOf(fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), dest, overwrite: true);
        return dest;
    }

    public string Write(string name, string content)
    {
        var path = PathOf(name);
        File.WriteAllText(path, content);
        return path;
    }

    public byte[] Bytes(string name) => File.ReadAllBytes(PathOf(name));

    public async Task<CliOutput> RunAsync(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = await CliApp.RunAsync(args, o, e);
        return new CliOutput(code, o.ToString(), e.ToString());
    }

    public async Task<CliOutput> RunWithStdinAsync(string stdin, params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = await CliApp.RunAsync(args, o, e, new StringReader(stdin));
        return new CliOutput(code, o.ToString(), e.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
    }
}

public sealed record CliOutput(int Code, string Out, string Err);
