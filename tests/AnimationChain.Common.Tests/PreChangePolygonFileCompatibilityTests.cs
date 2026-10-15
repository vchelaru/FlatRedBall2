using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// The .achx/.achj formats are shared with Gum and FRB1, so a file written before polygon point keyframes (#1484)
/// must load and re-save unchanged. <c>Fixtures/PreChangePolygon.*</c> were written by the writer at the parent
/// commit (origin/main before this change), not by the new code: three chains (two named "Walk", then "Idle"), each
/// with a polygon carrying points and the Offset keyframe fields from #1488, and a rectangle.
/// </summary>
public class PreChangePolygonFileCompatibilityTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), "PreChangePolygon-" + Guid.NewGuid().ToString("N") + extension);

    private static void AssertLoadedValues(AnimationChainListSave loaded)
    {
        loaded.AnimationChains.Select(c => c.Name).ShouldBe(new[] { "Walk", "Walk", "Idle" });
        foreach (var chain in loaded.AnimationChains)
        {
            chain.Frames.Count.ShouldBe(2);
            var first = chain.Frames[0].ShapesSave!.PolygonSaves.Single();
            first.Name.ShouldBe("Blade");
            first.Points.Select(p => (p.X, p.Y)).ShouldBe(new[] { (0f, 0f), (10.5f, 0f), (10.5f, -7f) });
            first.OffsetInterpolation.ShouldBe("Linear");
            first.OffsetEasing.ShouldBe("InOut");
            first.SkipOffsetKeyframe.ShouldBeFalse();
            chain.Frames[1].ShapesSave!.PolygonSaves.Single().SkipOffsetKeyframe.ShouldBeTrue();
            foreach (var poly in chain.Frames.Select(f => f.ShapesSave!.PolygonSaves.Single()))
            {
                poly.SkipPointsKeyframe.ShouldBeFalse();
                poly.PointsInterpolation.ShouldBeNull();
                poly.PointsEasing.ShouldBeNull();
            }
        }
    }

    [Fact]
    public void Achx_PreChangeFile_LoadsWithSnapPoints_AndResavesByteIdentical()
    {
        var loaded = AnimationChainListSave.FromFile(Fixture("PreChangePolygon.achx"));
        AssertLoadedValues(loaded);
        var resaved = TempPath(".achx");
        try
        {
            loaded.Save(resaved);

            File.ReadAllBytes(resaved).ShouldBe(File.ReadAllBytes(Fixture("PreChangePolygon.achx")));
        }
        finally { File.Delete(resaved); }
    }

    [Fact]
    public void Achj_PreChangeFile_LoadsWithSnapPoints_AndResavesIdentical()
    {
        var loaded = AnimationChainListSave.FromFile(Fixture("PreChangePolygon.achj"));
        AssertLoadedValues(loaded);
        var resaved = TempPath(".achj");
        try
        {
            loaded.SaveJson(resaved);

            // The JSON writer's newline follows the platform, so compare line endings normalized.
            Normalize(File.ReadAllText(resaved)).ShouldBe(Normalize(File.ReadAllText(Fixture("PreChangePolygon.achj"))));
        }
        finally { File.Delete(resaved); }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");
}
