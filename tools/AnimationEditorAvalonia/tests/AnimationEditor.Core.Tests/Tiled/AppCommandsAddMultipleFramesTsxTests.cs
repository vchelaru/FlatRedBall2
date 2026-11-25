using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>Add Multiple Frames on a .tsx steps by the tileset's own grid, so every frame stays a tile the save can write (#2051).</summary>
[Collection("SequentialSingletons")]
public class AppCommandsAddMultipleFramesTsxTests : IDisposable
{
    private const int Columns = 5;
    private const int Rows = 4;

    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    private FilePath OpenTsx(int margin, int spacing, string tiles)
    {
        int width = 2 * margin + Columns * 16 + (Columns - 1) * spacing;
        int height = 2 * margin + Rows * 16 + (Rows - 1) * spacing;

        var png = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(png, 0);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), height);
        File.WriteAllBytes(Path.Combine(_dir.Path, "Heroes.png"), png);

        var path = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" spacing="{spacing}" margin="{margin}" tilecount="{Columns * Rows}" columns="{Columns}">
             <image source="Heroes.png" width="{width}" height="{height}"/>
            {tiles}
            </tileset>
            """);
        _ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        return new FilePath(path);
    }

    private (int Column, int Row, int Columns, int Rows)[] SaveAndReloadCells(FilePath path)
    {
        _ctx.ProjectManager.SaveTsxProject().ShouldBeEmpty();

        _ctx.ProjectManager.LoadTsxProject(path);
        var grid = _ctx.ProjectManager.TsxTileGrid!.Value;
        return _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Frames
            .Select(f => grid.TryLocate(f.LeftCoordinate, f.TopCoordinate,
                f.RightCoordinate - f.LeftCoordinate, f.BottomCoordinate - f.TopCoordinate, 0.001f)!.Value)
            .Select(c => (c.Column, c.Row, c.Columns, c.Rows)).ToArray();
    }

    // Margin 12 is over half a tile: the right margin must not count as one more column.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(3, 0)]
    [InlineData(12, 1)]
    public void AddMultipleFrames_OneTileFrames_FollowTheGridAcrossARowWrapAndSave(int margin, int spacing)
    {
        var path = OpenTsx(margin, spacing, """
             <tile id="2">
              <animation>
               <frame tileid="2" duration="200"/>
              </animation>
             </tile>
            """);

        _ctx.AppCommands.AddMultipleFrames(
            _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(), 4, incrementUV: true).ShouldBeFalse();

        SaveAndReloadCells(path).ShouldBe([(2, 0, 1, 1), (3, 0, 1, 1), (4, 0, 1, 1), (0, 1, 1, 1), (1, 1, 1, 1)]);
    }

    // Five columns hold two 2-tile frames with one column left over, and the second case starts on an odd column.
    [Theory]
    [InlineData(0, new[] { 0, 2, 0 }, new[] { 0, 0, 1 })]
    [InlineData(1, new[] { 1, 3, 0 }, new[] { 0, 0, 1 })]
    public void AddMultipleFrames_TwoTileFrames_StepByTheFootprintAndSave(int tile, int[] columns, int[] rows)
    {
        var path = OpenTsx(3, 2, $"""
             <tile id="{tile}">
              <animation>
               <frame tileid="{tile}" duration="200"/>
              </animation>
             </tile>
             <tile id="{tile + 1}">
              <properties>
               <property name="ParentId" type="int" value="{tile}"/>
              </properties>
              <animation>
               <frame tileid="{tile + 1}" duration="200"/>
              </animation>
             </tile>
            """);

        _ctx.AppCommands.AddMultipleFrames(
            _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single(), 2, incrementUV: true).ShouldBeFalse();

        SaveAndReloadCells(path).ShouldBe(columns.Zip(rows, (c, r) => (c, r, 2, 1)).ToArray());
    }

    [Fact]
    public void AddMultipleFrames_PastTheLastTile_ReportsItAndFillsTheSheetFirst()
    {
        var path = OpenTsx(3, 2, """
             <tile id="18">
              <animation>
               <frame tileid="18" duration="200"/>
              </animation>
             </tile>
            """);
        var chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();

        _ctx.AppCommands.AddMultipleFrames(chain, 1, incrementUV: true).ShouldBeFalse();
        SaveAndReloadCells(path).ShouldBe([(3, 3, 1, 1), (4, 3, 1, 1)]);

        chain = _ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        _ctx.AppCommands.AddMultipleFrames(chain, 1, incrementUV: true).ShouldBeTrue();
    }
}
