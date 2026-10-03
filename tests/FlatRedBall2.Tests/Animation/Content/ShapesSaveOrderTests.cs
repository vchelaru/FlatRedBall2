using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.AnimationEditorCommon;

// The file writers group shapes by type (rects, polygons, circles). ShapesSave keeps its in-memory
// list in that same grouping so the order a user sees never changes across a save and reload (#1293).
public class ShapesSaveOrderTests
{
    private static AARectSave Rect(string n) => new() { Name = n };
    private static CircleSave Circle(string n) => new() { Name = n };
    private static PolygonSave Poly(string n) => new() { Name = n };

    private static string[] Names(ShapesSave s) => s.Shapes.Cast<ShapeSave>().Select(x => x.Name).ToArray();

    private static ShapesSave Mixed()
    {
        var s = new ShapesSave();
        s.Add(Circle("c1"));
        s.Add(Rect("r1"));
        s.Add(Poly("p1"));
        s.Add(Circle("c2"));
        s.Add(Rect("r2"));
        return s;
    }

    private static ShapesSave Reload(ShapesSave s, bool json)
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = s });
        save.AnimationChains.Add(chain);
        var back = json
            ? AnimationChainListSave.FromJsonString(save.ToJsonString())
            : AnimationChainListSave.FromString(save.ToXmlString());
        return back.AnimationChains[0].Frames[0].ShapesSave!;
    }

    [Fact]
    public void Add_MixedTypes_AppendsToEndOfOwnTypeGroup()
    {
        Names(Mixed()).ShouldBe(["r1", "r2", "p1", "c1", "c2"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_MixedTypes_OrderSurvivesSaveAndReload(bool json)
    {
        var s = Mixed();
        Names(Reload(s, json)).ShouldBe(Names(s));
    }

    [Fact]
    public void Insert_IndexOutsideTypeGroup_ClampsIntoGroup()
    {
        var s = Mixed();
        s.Insert(0, Circle("c0"));
        Names(s).ShouldBe(["r1", "r2", "p1", "c0", "c1", "c2"]);
    }

    [Fact]
    public void Move_AtGroupEdge_DoesNotCrossType()
    {
        var s = Mixed();
        s.Move(s.Shapes[1], +1).ShouldBeFalse();
        s.Move(s.Shapes[0], -1).ShouldBeFalse();
        Names(s).ShouldBe(["r1", "r2", "p1", "c1", "c2"]);
    }

    [Fact]
    public void Move_WithinGroup_Swaps()
    {
        var s = Mixed();
        s.Move(s.Shapes[3], +1).ShouldBeTrue();
        Names(s).ShouldBe(["r1", "r2", "p1", "c2", "c1"]);
    }

    [Fact]
    public void MoveToEdge_StaysInOwnGroup()
    {
        var s = Mixed();
        s.MoveToEdge(s.Shapes[4], toStart: true);
        Names(s).ShouldBe(["r1", "r2", "p1", "c2", "c1"]);
        s.MoveToEdge(s.Shapes[0], toStart: false);
        Names(s).ShouldBe(["r2", "r1", "p1", "c2", "c1"]);
    }

    [Fact]
    public void SetOrder_RejectsOrderThatMixesTypeGroups()
    {
        var s = Mixed();
        var reversed = s.Shapes.Reverse().ToArray();
        Should.Throw<System.ArgumentException>(() => s.SetOrder(reversed));
    }
}
