using System;
using System.Diagnostics;
using System.IO;
using Shouldly;

namespace AnimationEditor.Tests.Shared;

/// <summary>Takes away the right to list a folder (as <c>System Volume Information</c> has none) until disposed.</summary>
internal sealed class UnlistableFolder : IDisposable
{
    private readonly string _path;

    public UnlistableFolder(string path)
    {
        _path = Directory.CreateDirectory(path).FullName;
        if (OperatingSystem.IsWindows()) Icacls(_path, "/deny", Environment.UserName + ":(RD)");
        else File.SetUnixFileMode(_path, UnixFileMode.None);
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows()) Icacls(_path, "/remove:d", Environment.UserName);
        else File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void Icacls(params string[] arguments)
    {
        ProcessStartInfo start = new ProcessStartInfo("icacls") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0);
    }
}
