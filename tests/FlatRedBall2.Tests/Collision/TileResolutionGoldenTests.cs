using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Snapshot ("golden") push maps. For each layout and actor the file under TileResolutionGolden/ draws,
// for a grid of start positions, how the actor is pushed out. Unlike the invariant tests this pins the
// exact current behavior everywhere at once, and a diff of the file shows WHERE a change moved things.
//
// Legend (one character per start position, row 0 at the top = highest y):
//   .  actor does not overlap any tile              0  overlaps but the first pass does not move it
//   <  >  ^  v  first pass pushes left / right / up / down
//   1  2  3  4  first pass pushes diagonally: 1 = up-right, 2 = up-left, 3 = down-left, 4 = down-right
// A start that is still overlapping a tile after 12 passes ("stuck") is drawn with the stuck glyph instead:
//   (  )  A  V  for <  >  ^  v      a  b  c  d  for 1  2  3  4      !  for 0
// To regenerate after an intentional behavior change: set FRB_UPDATE_TILE_GOLDEN=1 and run these tests.
public class TileResolutionGoldenTests
{
    private const float Step = 4f;
    private const float Margin = 8f;
    private const int RestPasses = 12;

    private static readonly string[] ActorIds = { "sq4", "sq8", "sq12", "circ8", "circ12", "rect6x14" };

    // Layouts that contain fully surrounded tiles also get a second snapshot after AssignSurroundedTilesSolidOutward.
    private static readonly HashSet<string> WithSurroundedPass = new() { "block3x3", "block7x7", "thickfloor", "stairs", "cave" };

    public static IEnumerable<object[]> Maps() => TileResolutionMaps.Names();

    [Theory]
    [MemberData(nameof(Maps))]
    public void PushMap_MatchesTheSnapshot(string map)
    {
        var sb = new StringBuilder();
        AppendMode(sb, map, applySurroundedPass: false);
        if (WithSurroundedPass.Contains(map))
            AppendMode(sb, map, applySurroundedPass: true);
        string actual = sb.ToString();

        string path = GoldenPath(map);
        if (Environment.GetEnvironmentVariable("FRB_UPDATE_TILE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).ShouldBeTrue($"missing snapshot {path}; set FRB_UPDATE_TILE_GOLDEN=1 and rerun to create it");
        string expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected == actual) return;

        var e = expected.Split('\n');
        var a = actual.Split('\n');
        int firstDiff = Enumerable.Range(0, System.Math.Max(e.Length, a.Length))
            .First(i => i >= e.Length || i >= a.Length || e[i] != a[i]);
        int from = System.Math.Max(0, firstDiff - 2);
        string Excerpt(string[] lines) => string.Join("\n", lines.Skip(from).Take(24));
        throw new Xunit.Sdk.XunitException(
            $"Push map for '{map}' differs from {Path.GetFileName(path)} starting at line {firstDiff + 1}.\n" +
            $"--- expected (lines {from + 1}+)\n{Excerpt(e)}\n--- actual\n{Excerpt(a)}");
    }

    private static string GoldenPath(string map, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "TileResolutionGolden", map + ".txt");

    private static void AppendMode(StringBuilder sb, string map, bool applySurroundedPass)
    {
        var rows = TileResolutionMaps.Get(map);
        sb.Append("==== ").Append(map).Append(applySurroundedPass ? " (after AssignSurroundedTilesSolidOutward)" : "").Append('\n');
        foreach (var id in ActorIds)
        {
            var spec = TileResolutionActors.Get(id);
            if (!TileResolutionActors.Fits(map, spec)) continue;
            sb.Append(Section(map, rows, id, spec, applySurroundedPass));
        }
    }

    private static string Section(string map, string[] rows, string id, ActorSpec spec, bool applySurroundedPass)
    {
        var tiles = TileWorld.Map(rows);
        if (applySurroundedPass) tiles.AssignSurroundedTilesSolidOutward();
        var (w, h) = TileResolutionMaps.Size(map);

        var xs = new List<float>();
        for (float x = -Margin + 0.61f; x <= w * 16f + Margin; x += Step) xs.Add(x);
        var ys = new List<float>();
        for (float y = h * 16f + Margin + 0.37f; y >= -Margin; y -= Step) ys.Add(y);

        var grid = new char[ys.Count][];
        int starts = 0, stuck = 0;
        double firstSum = 0, restSum = 0;
        for (int r = 0; r < ys.Count; r++)
        {
            grid[r] = new char[xs.Count];
            for (int c = 0; c < xs.Count; c++)
            {
                float x = xs[c], y = ys[r];
                if (!TileWorld.Penetrates(spec, x, y, tiles)) { grid[r][c] = '.'; continue; }
                starts++;
                var passes = TileWorld.ResolvePasses(spec, tiles, x, y, RestPasses);
                var first = passes[0];
                var last = passes[^1];
                bool isStuck = TileWorld.Penetrates(spec, last.X, last.Y, tiles);
                if (isStuck) stuck++;
                firstSum += System.Math.Abs(first.X - x) + System.Math.Abs(first.Y - y);
                restSum += System.Math.Abs(last.X - x) + System.Math.Abs(last.Y - y);
                grid[r][c] = Glyph(first.X - x, first.Y - y, isStuck);
            }
        }

        // Crop rows/columns that are entirely '.' to keep the files small.
        var keepRows = Enumerable.Range(0, ys.Count).Where(r => grid[r].Any(ch => ch != '.')).ToList();
        var keepCols = Enumerable.Range(0, xs.Count).Where(c => keepRows.Any(r => grid[r][c] != '.')).ToList();

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append("-- ").Append(id).Append(' ').Append(spec)
          .Append(" starts=").Append(starts.ToString(inv))
          .Append(" stuck=").Append(stuck.ToString(inv))
          .Append(" firstPassDistance=").Append(firstSum.ToString("F1", inv))
          .Append(" restDistance=").Append(restSum.ToString("F1", inv)).Append('\n');
        if (keepRows.Count > 0)
        {
            sb.Append("   x from ").Append(xs[keepCols[0]].ToString("F2", inv)).Append(" step ").Append(Step.ToString("F0", inv))
              .Append(", y from ").Append(ys[keepRows[0]].ToString("F2", inv)).Append(" down step ").Append(Step.ToString("F0", inv)).Append('\n');
            foreach (int r in keepRows)
            {
                sb.Append("   ");
                foreach (int c in keepCols) sb.Append(grid[r][c]);
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static char Glyph(float dx, float dy, bool stuck)
    {
        const float t = 0.01f;
        bool hx = System.Math.Abs(dx) > t, hy = System.Math.Abs(dy) > t;
        if (!hx && !hy) return stuck ? '!' : '0';
        if (hx && hy)
        {
            if (dx > 0 && dy > 0) return stuck ? 'a' : '1';
            if (dx < 0 && dy > 0) return stuck ? 'b' : '2';
            if (dx < 0 && dy < 0) return stuck ? 'c' : '3';
            return stuck ? 'd' : '4';
        }
        if (hx) return dx > 0 ? (stuck ? ')' : '>') : (stuck ? '(' : '<');
        return dy > 0 ? (stuck ? 'A' : '^') : (stuck ? 'V' : 'v');
    }
}
