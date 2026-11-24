using System.Text.Json;
using AnimationEditor.Editing;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>A batch that fails after it copied a texture leaves every file in the folder as it was (#1978).</summary>
public class EditRollbackTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "broken.achx", "sheet.png", "other.png", "tiles.tsx");
    private readonly string _outside;
    private readonly string _far;

    public EditRollbackTests()
    {
        _outside = _w.Dir + "_outside";
        Directory.CreateDirectory(_outside);
        _far = Path.Combine(_outside, "far.png");
        // sheet.png and other.png hold the same bytes, so this one gets a byte past the image's end.
        File.WriteAllBytes(_far, [.. _w.Bytes("other.png"), 0]);
    }

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(_w.Dir)) File.SetAttributes(file, FileAttributes.Normal);
        _w.Dispose();
        File.SetAttributes(_far, FileAttributes.Normal);
        Directory.Delete(_outside, recursive: true);
    }

    /// <summary>Runs <paramref name="ops"/> with <c>FAR</c> standing for the texture outside the folder.</summary>
    private Task<CliOutput> Edit(params string[] ops) => _w.RunAsync(
        ["edit", _w.PathOf("hero.achx"), "--json", .. ops.SelectMany(o => new[] { "--op", o.Replace("FAR", JsonSerializer.Serialize(_far)) })]);

    private List<string> Snapshot() => Directory.GetFiles(_w.Dir).Order().Select(f =>
        $"{Path.GetFileName(f)} {File.GetLastWriteTimeUtc(f):O} {File.GetAttributes(f)} {Convert.ToHexString(File.ReadAllBytes(f))}").ToList();

    // A different far.png next to the file, old enough that a copy changes its time.
    private string ExistingFar()
    {
        var path = _w.PathOf("far.png");
        File.Copy(_w.PathOf("sheet.png"), path);
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        return path;
    }

    private const string CopyingOp = "set-frame-texture Idle/0 texture=FAR copy=";

    public static TheoryData<string, string[]> FailingBatches()
    {
        var data = new TheoryData<string, string[]>();
        foreach (var copy in new[] { "copy", "overwrite" })
        {
            data.Add("NotFound", [CopyingOp + copy, "delete-chain Nope"]);
            data.Add("NoMatch", [CopyingOp + copy, "delete-chain Nope*"]);
            data.Add("InvalidValue", [CopyingOp + copy, "associate-tileset nope.txt"]);
            data.Add("WrongFileKind", [CopyingOp + copy, "set-chain-owner-tile Idle tile=1"]);
            data.Add("Locked", ["set-chain-locked Idle value=true", $"set-frame-texture */* texture=FAR copy={copy}"]);
            data.Add("Locked", ["set-chain-locked Idle value=true", $"add-frame Idle texture=FAR copy={copy}"]);
            data.Add("Locked", ["set-chain-locked Idle value=true", $"add-frames-grid Idle texture=FAR cellWidth=16 cellHeight=32 columns=3 rows=1 copy={copy}"]);
            data.Add("InvalidValue", [$"add-frame Idle texture=FAR region=40,0,16,4 copy={copy}"]);
            data.Add("DuplicateName", [CopyingOp + copy, $"add-frame Idle texture=FAR copy={copy}", "add-chain Walk"]);
            data.Add("WouldBreakFile", [CopyingOp + copy, "import-chain Walk from=broken.achx name=Torn"]);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(FailingBatches))]
    public async Task Edit_FailsAfterACopy_LeavesTheFolderAsItWas(string code, string[] ops)
    {
        if (ops.Any(o => o.Contains("copy=overwrite"))) ExistingFar();
        var before = Snapshot();

        var r = await Edit(ops);

        r.Code.ShouldBe(1, r.Out + r.Err);
        r.Out.ShouldContain($"\"{code}\"");
        Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task Edit_OverwriteOfASheetAnotherChainUses_FailingLater_PutsTheSheetBack()
    {
        var source = Path.Combine(_outside, "sheet.png");
        File.Copy(_far, source);
        var before = Snapshot();

        var r = await Edit($"set-frame-texture Idle/0 texture={JsonSerializer.Serialize(source)} copy=overwrite", "delete-chain Nope");

        r.Code.ShouldBe(1, r.Out + r.Err);
        Snapshot().ShouldBe(before);
    }

    // A read-only source makes a read-only copy.
    [Theory]
    [InlineData("copy")]
    [InlineData("overwrite")]
    public async Task Edit_FailsAfterCopyingAReadOnlySource_LeavesTheFolderAsItWas(string copy)
    {
        File.SetAttributes(_far, FileAttributes.ReadOnly);
        if (copy == "overwrite") ExistingFar();
        var before = Snapshot();

        var r = await Edit(CopyingOp + copy, "delete-chain Nope");

        r.Code.ShouldBe(1, r.Out + r.Err);
        Snapshot().ShouldBe(before);
    }

    [Theory]
    [InlineData("set-frame-texture Idle/0 texture=FAR copy=copy")]
    [InlineData("set-frame-texture Idle/0 texture=FAR copy=overwrite")]
    [InlineData("associate-tileset tiles.tsx")]
    public async Task Edit_FailedSave_LeavesTheFolderAsItWas(string op)
    {
        if (op.Contains("overwrite")) ExistingFar();
        SetWritable(false);
        try
        {
            var before = Snapshot();

            var r = await Edit(op);

            r.Code.ShouldBe(1, r.Out + r.Err);
            Snapshot().ShouldBe(before);
        }
        finally { SetWritable(true); }
    }

    [Fact]
    public async Task Edit_ReadOnlyDestination_FailsAndChangesNothing()
    {
        File.SetAttributes(ExistingFar(), FileAttributes.ReadOnly);
        var before = Snapshot();

        var r = await Edit(CopyingOp + "overwrite");

        r.Out.ShouldContain("TextureCopyFailed");
        Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task Edit_DestinationInUse_FailsAndUndoesTheEarlierCopy()
    {
        var other = Path.Combine(_outside, "held.png");
        File.Copy(_far, other);
        var held = _w.Write("held.png", "someone else's");
        var before = Snapshot();

        CliOutput r;
        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            r = await Edit(CopyingOp + "copy", $"set-frame-texture Walk/0 texture={JsonSerializer.Serialize(other)} copy=overwrite");

        r.Out.ShouldContain("TextureCopyFailed");
        Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task Edit_SuccessfulOverwrites_KeepTheCopy()
    {
        var existing = ExistingFar();

        var r = await Edit("set-frame-texture */* texture=FAR copy=overwrite", "add-frame Idle texture=FAR copy=copy");

        r.Code.ShouldBe(0, r.Out + r.Err);
        File.ReadAllBytes(existing).ShouldBe(File.ReadAllBytes(_far));
    }

    private void SetWritable(bool writable)
    {
        // A save moves a temp file over the old one, which a read-only file stops on Windows and a read-only folder stops elsewhere.
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(_w.PathOf("hero.achx"), writable ? FileAttributes.Normal : FileAttributes.ReadOnly);
            return;
        }
        var mode = UnixFileMode.UserRead | UnixFileMode.UserExecute;
        File.SetUnixFileMode(_w.Dir, writable ? mode | UnixFileMode.UserWrite : mode);
    }
}
