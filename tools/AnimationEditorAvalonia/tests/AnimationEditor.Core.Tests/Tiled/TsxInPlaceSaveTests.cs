using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Tiled;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>What a save of an open <c>.tsx</c> starts from (#2013), and the paths a Save As writes (#2016).</summary>
public sealed class TsxInPlaceSaveTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private const string Xml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset name=\"Heroes\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n" +
        " <image source=\"Heroes.png\" width=\"64\" height=\"64\"/>\n" +
        " <tile id=\"0\">\n" +
        "  <animation>\n" +
        "   <frame tileid=\"0\" duration=\"200\"/>\n" +
        "  </animation>\n" +
        " </tile>\n" +
        " <!-- kept -->\n" +
        " <tile id=\"8\" future=\"1\">\n" +
        "  <properties>\n" +
        "   <property name=\"Name\" value=\"Run\"/>\n" +
        "  </properties>\n" +
        "  <animation x='1'>\n" +
        "   <frame duration=\"150\" tileid=\"8\" y=\"1\"/>\n" +
        "  </animation>\n" +
        " </tile>\n" +
        "</tileset>\n";

    private string Write(string xml, string name = "Heroes.tsx")
    {
        var path = Path.Combine(_dir.Path, name);
        File.WriteAllText(path, xml);
        return path;
    }

    private static ProjectManager Open(string path)
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(path));
        return pm;
    }

    private static void SetFirstDuration(ProjectManager pm, int milliseconds) =>
        pm.AnimationChainListSave!.AnimationChains[0].Frames[0].FrameLength = milliseconds / 1000f;

    [Fact]
    public void SaveTsxProject_EditsUndone_FileHoldsTheOpenedBytes()
    {
        var path = Write(Xml);
        var pm = Open(path);
        var chains = pm.AnimationChainListSave!.AnimationChains;
        var deleted = chains.ToList();
        chains.Clear();
        pm.SaveTsxProject();
        File.ReadAllText(path).ShouldNotContain("animation");

        chains.AddRange(deleted);
        pm.SaveTsxProject();

        File.ReadAllText(path).ShouldBe(Xml);
    }

    // Tiled is usually open on the same file, and the reload of what it wrote runs a moment later.
    [Fact]
    public void SaveTsxProject_FileChangedOnDiskBeforeTheReload_KeepsThatChange()
    {
        var path = Write(Xml);
        var pm = Open(path);
        SetFirstDuration(pm, 300);
        pm.SaveTsxProject();
        // Another tile's animation, and a tile the editor does not know yet.
        var disk = File.ReadAllText(path).Replace("duration=\"150\"", "duration=\"151\"").Replace(
            "</tileset>", " <tile id=\"9\">\n  <animation>\n   <frame tileid=\"9\" duration=\"1\"/>\n  </animation>\n </tile>\n <wangsets/>\n</tileset>");
        File.WriteAllText(path, disk);

        SetFirstDuration(pm, 400);
        pm.SaveTsxProject();
        File.ReadAllText(path).ShouldBe(disk.Replace("duration=\"300\"", "duration=\"400\""));

        SetFirstDuration(pm, 200);
        pm.SaveTsxProject();
        File.ReadAllText(path).ShouldBe(disk.Replace("duration=\"300\"", "duration=\"200\""));
    }

    [Theory]
    [InlineData("<tileset")]
    [InlineData("<tileset name=\"Heroes\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\"/>")]
    public void SaveTsxProject_FileOnDiskNoLongerOpens_IsRefusedAndLeftAlone(string disk)
    {
        var path = Write(Xml);
        var pm = Open(path);
        SetFirstDuration(pm, 300);
        File.WriteAllText(path, disk);

        Should.Throw<InvalidOperationException>(() => pm.SaveTsxProject())
            .Message.ShouldStartWith("\"Heroes.tsx\" changed on disk and couldn't be reloaded (");

        File.ReadAllText(path).ShouldBe(disk);
    }

    [Fact]
    public void SaveTsxProject_FileDeletedOnDisk_IsWrittenAgain()
    {
        var path = Write(Xml);
        var pm = Open(path);
        File.Delete(path);

        pm.SaveTsxProject();

        File.ReadAllText(path).ShouldBe(Xml);
    }

    [Fact]
    public async Task SaveAs_AnotherFolder_OpenTilesetKeepsItsTextureAndItsEdits()
    {
        var path = Write(Xml.Replace("duration=\"150\"", "duration=\"150\" z=\"\""));
        var opened = File.ReadAllText(path);
        var target = Path.Combine(Directory.CreateDirectory(Path.Combine(_dir.Path, "Copies", "Deep")).FullName, "Copy.tsx");
        var ctx = TestHelpers.SetupFreshAcls();
        await ctx.AppCommands.OpenTsxWorkflowAsync(path);
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        var expected = opened.Replace("\"Heroes.png\"", "\"../../Heroes.png\"");
        File.ReadAllText(target).ShouldBe(expected);
        var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
        chains.SelectMany(chain => chain.Frames).Select(frame => frame.TextureName).Distinct().ShouldBe(["../../Heroes.png"]);

        chains[0].Frames[0].FrameLength = 0.3f;
        ctx.AppCommands.SaveCurrentAnimationChainList();

        File.ReadAllText(target).ShouldBe(expected.Replace("duration=\"200\"", "duration=\"300\""));
        File.ReadAllText(path).ShouldBe(opened);
    }

    // The history holds frames that name the image from the old folder.
    [Theory]
    [InlineData("Copies/Copy.tsx", false)]
    [InlineData("Copy.tsx", true)]
    public async Task SaveAs_UndoHistory_IsKeptOnlyInTheSameFolder(string name, bool kept)
    {
        var path = Write(Xml);
        var target = Path.Combine(_dir.Path, name);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var ctx = TestHelpers.SetupFreshAcls();
        await ctx.AppCommands.OpenTsxWorkflowAsync(path);
        var run = ctx.ProjectManager.AnimationChainListSave!.AnimationChains[1];
        ctx.AppCommands.RenameChain(run, "Sprint");
        ctx.AppCommands.RenameChain(run, "Dash");
        ctx.UndoManager.Undo();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        (ctx.UndoManager.CanUndo, ctx.UndoManager.CanRedo).ShouldBe((kept, kept));
        ctx.UndoManager.SaveState.ShouldBe(SaveState.AutoSaveOn);
        ctx.UndoManager.Undo();
        ctx.AppCommands.SaveCurrentAnimationChainList();
        File.ReadAllText(target).ShouldContain(kept ? "\"Run\"" : "\"Sprint\"");
    }

    [Theory]
    [InlineData("tilewidth=\"16\"", "tilewidth=\"8\"")]
    [InlineData("tileheight=\"16\"", "tileheight=\"8\"")]
    [InlineData("tilecount=\"16\"", "tilecount=\"15\"")]
    [InlineData("columns=\"4\"", "columns=\"8\"")]
    [InlineData("columns=\"4\"", "columns=\"4\" spacing=\"1\"")]
    [InlineData("columns=\"4\"", "columns=\"4\" margin=\"1\"")]
    [InlineData("Heroes.png", "Other.png")]
    [InlineData("width=\"64\"", "width=\"128\"")]
    [InlineData("height=\"64\"", "height=\"128\"")]
    public void SaveTsxProject_GridOrImageChangedOnDiskBeforeTheReload_IsRefusedAndLeftAlone(string from, string to)
    {
        var path = Write(Xml);
        var pm = Open(path);
        SetFirstDuration(pm, 300);
        var disk = Xml.Replace(from, to);
        disk.ShouldNotBe(Xml);
        File.WriteAllText(path, disk);

        Should.Throw<InvalidOperationException>(() => pm.SaveTsxProject())
            .Message.ShouldStartWith("\"Heroes.tsx\" changed on disk and couldn't be reloaded (");

        File.ReadAllText(path).ShouldBe(disk);
    }

    [Fact]
    public void SaveTsxProject_FileChangedOnDiskThenTheLastEditUndone_PutsTheOpenedValueBack()
    {
        var path = Write(Xml);
        var pm = Open(path);
        SetFirstDuration(pm, 300);
        pm.SaveTsxProject();
        var disk = File.ReadAllText(path).Replace("duration=\"150\"", "duration=\"151\"");
        File.WriteAllText(path, disk);

        SetFirstDuration(pm, 200);
        pm.SaveTsxProject();

        File.ReadAllText(path).ShouldBe(disk.Replace("duration=\"300\"", "duration=\"200\""));
    }

    [Fact]
    public void MoveTsxProject_CopyChangedOnDisk_KeepsThatChange()
    {
        var pm = Open(Write(Xml));
        SetFirstDuration(pm, 300);
        var copy = Path.Combine(_dir.Path, "Copy.tsx");
        pm.SaveTsxProject(copy);
        pm.MoveTsxProject(new FilePath(copy));
        var disk = File.ReadAllText(copy).Replace("duration=\"150\"", "duration=\"151\"");
        File.WriteAllText(copy, disk);

        SetFirstDuration(pm, 200);
        pm.SaveTsxProject(copy);

        File.ReadAllText(copy).ShouldBe(disk.Replace("duration=\"300\"", "duration=\"200\""));
    }

    [Fact]
    public void MoveTsxProject_CopyNotChangedOnDisk_StillStartsFromTheOpenedText()
    {
        var pm = Open(Write(Xml));
        var chains = pm.AnimationChainListSave!.AnimationChains;
        var deleted = chains.ToList();
        chains.Clear();
        var copy = Path.Combine(_dir.Path, "Copy.tsx");
        pm.SaveTsxProject(copy);
        pm.MoveTsxProject(new FilePath(copy));

        chains.AddRange(deleted);
        pm.SaveTsxProject(copy);

        File.ReadAllText(copy).ShouldBe(Xml);
    }

    [Fact]
    public async Task SaveAs_TargetCannotBeWritten_DocumentStaysAtItsFile()
    {
        var path = Write(Xml);
        var target = Path.Combine(Directory.CreateDirectory(Path.Combine(_dir.Path, "Copies")).FullName, "Copy.tsx");
        // A folder in the way fails the write on every platform.
        Directory.CreateDirectory(target);
        var ctx = TestHelpers.SetupFreshAcls();
        await ctx.AppCommands.OpenTsxWorkflowAsync(path);
        string? failure = null;
        ctx.AppCommands.SaveFailed += message => failure = message;
        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);

        await ctx.AppCommands.SaveCurrentAnimationChainListAsync();

        failure.ShouldNotBeNull();
        new FilePath(ctx.ProjectManager.FileName!).ShouldBe(new FilePath(path));
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0].Frames[0].TextureName.ShouldBe("Heroes.png");
    }

    /// <summary>The reload that takes another program's change runs even when a save went into that change first.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_RealWatcher_ReloadsOnlyWhenAnotherProgramChangedTheFile(bool changedOutside)
    {
        var path = Write(Xml);
        var ctx = TestHelpers.SetupFreshAcls();
        using var watcher = new HotReloadWatcher();
        var reports = 0;
        watcher.AchxChangedOnDisk += _ => System.Threading.Interlocked.Increment(ref reports);
        ctx.AppCommands.HotReloadWatcher = watcher;
        ctx.AppCommands.AutosaveOnChange = true;
        await ctx.AppCommands.OpenTsxWorkflowAsync(path);
        SetFirstDuration((ProjectManager)ctx.ProjectManager, 300);
        ctx.AppCommands.SaveCurrentAnimationChainList();
        await Task.Delay(600);
        reports.ShouldBe(0);

        if (changedOutside)
            File.WriteAllText(path, File.ReadAllText(path).Replace("duration=\"150\"", "duration=\"151\""));
        SetFirstDuration((ProjectManager)ctx.ProjectManager, 400);
        ctx.AppCommands.SaveCurrentAnimationChainList();
        for (var i = 0; i < 40 && reports == 0 && changedOutside; i++)
            await Task.Delay(100);
        await Task.Delay(600);

        reports.ShouldBe(changedOutside ? 1 : 0);
        File.ReadAllText(path).ShouldContain("duration=\"400\"");
    }

    [Theory]
    [InlineData("<image source=\"{0}\"/>", "art/Heroes.png", "../art/Heroes.png")]
    [InlineData("<image source=\"{0}\"/>", "../a &amp; b.png", "../../a &amp; b.png")]
    [InlineData("<image source='{0}'/>", "Copies/Heroes.png", "Heroes.png")]
    [InlineData("<image source=\"{0}\"/>", "/art/Heroes.png", null)]
    [InlineData("<image source=\"{0}\"/>", "C:/art/Heroes.png", null)]
    [InlineData("<image source=\"{0}\"/>", "//server/art/Heroes.png", null)]
    [InlineData("<image source=\"{0}\"/>", "http://example.com/Heroes.png", null)]
    [InlineData("<image source=\"a.png\"/><tile id=\"1\"><objectgroup><object template=\"{0}\"/></objectgroup></tile>", "Box.tx", "../Box.tx", "a.png", "../a.png")]
    [InlineData("<image source=\"a.png\"/><properties><property name=\"F\" type=\"file\" value=\"{0}\"/></properties>", "x/y.txt", "../x/y.txt", "a.png", "../a.png")]
    [InlineData("<image source=\"a.png\"/><properties><property name=\"F\" type=\"file\" value=\"{0}\"/></properties>", "", null, "a.png", "../a.png")]
    [InlineData("<image source=\"a.png\"/><properties><property name=\"L\" type=\"list\"><item type=\"file\" value=\"{0}\"/></property></properties>", "y.txt", "../y.txt", "a.png", "../a.png")]
    [InlineData("<image source=\"a.png\"/><editorsettings><export target=\"{0}\" format=\"json\"/></editorsettings>", "Heroes.json", "../Heroes.json", "a.png", "../a.png")]
    [InlineData("<image source=\"a.png\"/><properties><property name=\"S\" value=\"{0}\"/></properties>", "y.txt", null, "a.png", "../a.png")]
    public void SaveTsxProject_ToAnotherFolder_RebasesThePathsTiledResolvesAgainstTheFile(
        string content, string reference, string? rebased, string? image = null, string? rebasedImage = null)
    {
        static string Tileset(string inner) =>
            $"<tileset name=\"Heroes\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">{inner}</tileset>";
        var pm = Open(Write(Tileset(string.Format(content, reference))));
        var target = Path.Combine(Directory.CreateDirectory(Path.Combine(_dir.Path, "Copies")).FullName, "Copy.tsx");

        pm.SaveTsxProject(target);

        var expected = Tileset(string.Format(content, rebased ?? reference));
        File.ReadAllText(target).ShouldBe(image is null ? expected : expected.Replace($"source=\"{image}\"", $"source=\"{rebasedImage}\""));
    }

    [Fact]
    public void RebasePath_AnotherDrive_LeavesThePath()
    {
        if (!OperatingSystem.IsWindows()) return;

        TsxWriter.RebasePath("art/Heroes.png", new FilePath("C:/a/"), new FilePath("D:/a/")).ShouldBe("art/Heroes.png");
        TsxWriter.RebasePath("art/Heroes.png", new FilePath("C:/a/"), new FilePath("C:/b/")).ShouldBe("../a/art/Heroes.png");
    }
}
