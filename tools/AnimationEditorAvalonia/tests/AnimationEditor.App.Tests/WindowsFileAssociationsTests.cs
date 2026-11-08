using System;
using System.Linq;
using System.Runtime.Versioning;
using AnimationEditor.App.Services;
using Microsoft.Win32;
using Xunit;

namespace AnimationEditor.App.Tests;

public class WindowsFileAssociationsTests
{
    [Fact]
    public void BuildWrites_ListsCapabilitiesUnderRegisteredApplications()
    {
        var writes = WindowsFileAssociations.BuildWrites(@"C:\Users\me\AppData\Local\FlatRedBall2.AnimationEditor\current\AnimationEditor.exe");

        // Windows' Default apps page lists only apps named under RegisteredApplications.
        Assert.Contains(new RegistryValueWrite(
            @"Software\RegisteredApplications", "FlatRedBall AnimationEditor",
            @"Software\FlatRedBall\AnimationEditor\Capabilities"), writes);
        Assert.Contains(new RegistryValueWrite(
            @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".achx",
            "FlatRedBall.AnimationEditor.achx"), writes);
        Assert.Contains(new RegistryValueWrite(
            @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".achj",
            "FlatRedBall.AnimationEditor.achx"), writes);
    }

    [Fact]
    public void BuildWrites_OpenCommand_TargetsGivenExe()
    {
        string exe = @"C:\Users\me\AppData\Local\FlatRedBall2.AnimationEditor\current\AnimationEditor.exe";

        var writes = WindowsFileAssociations.BuildWrites(exe);

        var openCommand = writes.Single(w => w.SubKey == @"Software\Classes\FlatRedBall.AnimationEditor.achx\shell\open\command");
        Assert.Equal($"\"{exe}\" \"%1\"", openCommand.Value);
    }

    private const string Exe = @"C:\Users\me\AppData\Local\FlatRedBall2.AnimationEditor\current\AnimationEditor.exe";

    [Fact]
    public void BuildWrites_Seqj_HasOwnProgIdAndMapsExtension()
    {
        var writes = WindowsFileAssociations.BuildWrites(Exe);

        const string progId = "FlatRedBall.AnimationEditor.seqj";
        Assert.Contains(new RegistryValueWrite(@"Software\Classes\.seqj", null, progId), writes);
        Assert.Contains(new RegistryValueWrite(@"Software\Classes\.seqj\OpenWithProgids", progId, string.Empty), writes);
        Assert.Contains(new RegistryValueWrite(
            @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".seqj", progId), writes);
    }

    [Fact]
    public void BuildWrites_Seqj_OpenCommandAndIconTargetGivenExe()
    {
        var writes = WindowsFileAssociations.BuildWrites(Exe);

        string key = @"Software\Classes\FlatRedBall.AnimationEditor.seqj";
        Assert.Equal($"\"{Exe}\" \"%1\"", writes.Single(w => w.SubKey == key + @"\shell\open\command").Value);
        Assert.Equal($"\"{Exe}\",0", writes.Single(w => w.SubKey == key + @"\DefaultIcon").Value);
    }

    [Fact]
    public void FileTypes_ListsEachProgIdWithItsExtensions()
    {
        var types = WindowsFileAssociations.FileTypes;

        Assert.Equal(new[] { "FlatRedBall.AnimationEditor.achx", "FlatRedBall.AnimationEditor.seqj" },
            types.Select(t => t.ProgId));
        Assert.Equal(new[] { ".achx", ".achj" }, types[0].Extensions);
        Assert.Equal(new[] { ".seqj" }, types[1].Extensions);
    }

    [Fact]
    public void BuildWrites_ProgIdsAreDistinctPerType()
    {
        var progIdRoots = WindowsFileAssociations.BuildWrites(Exe)
            .Where(w => w.SubKey.StartsWith(@"Software\Classes\FlatRedBall.") && w.Name is null
                        && !w.SubKey.Contains(@"\shell") && !w.SubKey.Contains(@"\DefaultIcon"))
            .Select(w => w.Value);

        Assert.Equal(new[] { "FlatRedBall Animation Chain", "FlatRedBall Animation Sequence" }, progIdRoots);
    }

    // The tests below run the real Register/Unregister against a throwaway key under HKCU, never
    // the real Software\Classes, and delete it afterwards.
    [SupportedOSPlatform("windows")]
    private static void WithThrowawayHive(Action<string> body)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Registry writing is Windows-only.");
        string prefix = $@"Software\AnimationEditorTests\{Guid.NewGuid():N}\";
        try { body(prefix); }
        finally { Registry.CurrentUser.DeleteSubKeyTree(prefix.TrimEnd('\\'), throwOnMissingSubKey: false); }
    }

    [SupportedOSPlatform("windows")]
    private static object? ReadValue(string path, string? name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key?.GetValue(name ?? string.Empty);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Register_WritesSeqjKeys_AndUnregisterRemovesThem()
    {
        WithThrowawayHive(prefix =>
        {
            WindowsFileAssociations.Register(Exe, prefix);

            Assert.Equal("FlatRedBall.AnimationEditor.seqj", ReadValue(prefix + @"Software\Classes\.seqj", null));
            Assert.Equal($"\"{Exe}\" \"%1\"",
                ReadValue(prefix + @"Software\Classes\FlatRedBall.AnimationEditor.seqj\shell\open\command", null));
            Assert.NotNull(ReadValue(prefix + @"Software\Classes\.seqj\OpenWithProgids", "FlatRedBall.AnimationEditor.seqj"));
            Assert.Equal("FlatRedBall.AnimationEditor.seqj",
                ReadValue(prefix + @"Software\FlatRedBall\AnimationEditor\Capabilities\FileAssociations", ".seqj"));

            WindowsFileAssociations.Unregister(prefix);

            Assert.Null(ReadValue(prefix + @"Software\Classes\.seqj", null));
            Assert.Null(ReadValue(prefix + @"Software\Classes\.seqj\OpenWithProgids", "FlatRedBall.AnimationEditor.seqj"));
            Assert.Null(ReadValue(prefix + @"Software\Classes\FlatRedBall.AnimationEditor.seqj\shell\open\command", null));
            Assert.Null(ReadValue(prefix + @"Software\Classes\FlatRedBall.AnimationEditor.achx\shell\open\command", null));
            Assert.Null(ReadValue(prefix + @"Software\RegisteredApplications", WindowsFileAssociations.RegisteredAppName));
            using var appKey = Registry.CurrentUser.OpenSubKey(prefix + @"Software\FlatRedBall\AnimationEditor");
            Assert.Null(appKey);
        });
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Unregister_KeepsSeqjMappingOfAnotherApp()
    {
        WithThrowawayHive(prefix =>
        {
            WindowsFileAssociations.Register(Exe, prefix);
            using (var key = Registry.CurrentUser.CreateSubKey(prefix + @"Software\Classes\.seqj"))
                key.SetValue(string.Empty, "Other.App.seqj");

            WindowsFileAssociations.Unregister(prefix);

            Assert.Equal("Other.App.seqj", ReadValue(prefix + @"Software\Classes\.seqj", null));
        });
    }
}
