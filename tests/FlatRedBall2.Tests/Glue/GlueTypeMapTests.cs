using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

public class GlueTypeMapTests
{
    [Fact]
    public void TryGetType_AxisAlignedRectangle_MapsToAARectDespiteTheRename()
    {
        GlueTypeMap.TryGetType("FlatRedBall.Math.Geometry.AxisAlignedRectangle", out var type).ShouldBeTrue();

        type.ShouldBe(typeof(FlatRedBall2.Collision.AARect));
    }

    [Fact]
    public void TryGetType_ElementReference_DoesNotResolveAsAType()
    {
        // "Entities\Player" names a Screen or Entity in the project, which Phase 2 instantiates from
        // its own data rather than by looking up a CLR type.
        GlueTypeMap.TryGetType(@"Entities\Player", out var type).ShouldBeFalse();

        type.ShouldBeNull();
    }

    [Fact]
    public void TryGetType_LaterPhaseType_ReportsUnmapped()
    {
        GlueTypeMap.TryGetType("FlatRedBall.TileCollisions.TileShapeCollection", out _).ShouldBeFalse();
        GlueTypeMap.TryGetType("FlatRedBall.Graphics.Text", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryGetType_Line_MapsToCollisionLine()
    {
        GlueTypeMap.TryGetType("FlatRedBall.Math.Geometry.Line", out var type).ShouldBeTrue();

        type.ShouldBe(typeof(FlatRedBall2.Collision.Line));
    }

    [Fact]
    public void TryGetType_PositionedObject_MapsToEntity()
    {
        // A bare PositionedObject is an attachment anchor with no visual — FRB2's Entity is the
        // equivalent: it has position/attachment but no required shape or sprite.
        GlueTypeMap.TryGetType("FlatRedBall.PositionedObject", out var type).ShouldBeTrue();

        type.ShouldBe(typeof(FlatRedBall2.Entity));
    }

    [Fact]
    public void TryGetType_Sprite_MapsToRenderingSprite()
    {
        GlueTypeMap.TryGetType("FlatRedBall.Sprite", out var type).ShouldBeTrue();

        type.ShouldBe(typeof(FlatRedBall2.Rendering.Sprite));
    }

    [Fact]
    public void TryGetType_UnresolvedGenericList_MatchesOnOpenNameNotWholeString()
    {
        // Not mapped in this phase, but it must fail by open-name lookup rather than by the literal
        // string "...PositionedObjectList<T>" never matching anything.
        var parsed = GlueTypeName.Parse("FlatRedBall.Math.PositionedObjectList<T>");

        parsed.OpenTypeName.ShouldBe("FlatRedBall.Math.PositionedObjectList");
        GlueTypeMap.TryGetType(parsed, out _).ShouldBeFalse();
    }

    [Fact]
    public void Load_DoorsDemo_HasNoUnmappedTypeWarningsAndNoErrors()
    {
        // Pinning the count turns it into a progress metric: each phase that lands should drive it
        // down. Under a fail-fast policy this project could not load at all.
        // Dropped to 0 once #1073 exempted PositionedObjectList<T> from this report the same way tile
        // and collision objects already were -- every "cannot be built" warning this fixture produced
        // was a list, which already builds correctly.
        var glujPath = Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj");

        var result = GlueProjectLoader.Load(glujPath);

        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.Count(d => d.Message.Contains("cannot be built by this build")).ShouldBe(0);
    }

    // FileVersion 54 projects write SourceClassType in short form, and mix both spellings inside one
    // file. FRB1's own test project has 110 files using a short name and 7 that use both -- so a map
    // keyed only on the qualified form silently fails to build most of that project's objects.
    [Theory]
    [InlineData("Sprite", typeof(FlatRedBall2.Rendering.Sprite))]
    [InlineData("AxisAlignedRectangle", typeof(FlatRedBall2.Collision.AARect))]
    [InlineData("Circle", typeof(FlatRedBall2.Collision.Circle))]
    [InlineData("Polygon", typeof(FlatRedBall2.Collision.Polygon))]
    public void TryGetType_AShortFormTypeName_ResolvesTheSameAsTheQualifiedOne(
        string shortName, Type expected)
    {
        GlueTypeMap.TryGetType(GlueTypeName.Parse(shortName), out var type).ShouldBeTrue();
        type.ShouldBe(expected);
    }

    [Fact]
    public void TryCreate_AShortFormTypeName_ConstructsTheSameInstance()
    {
        GlueTypeMap.TryCreate(GlueTypeName.Parse("Circle"), out object? instance).ShouldBeTrue();
        instance.ShouldBeOfType<FlatRedBall2.Collision.Circle>();
    }

    // An element reference is still not a type, whichever spelling is accepted. Element names carry
    // a backslash, so they are rejected before the alias table is consulted.
    [Fact]
    public void TryGetType_AnElementReference_IsStillNotAType()
    {
        GlueTypeMap.TryGetType(GlueTypeName.Parse(@"Entities\Sprite"), out _).ShouldBeFalse();
    }

    [Fact]
    public void Load_DoorsDemo_DoesNotReportNestedEntityInstancesAsUnmapped()
    {
        // PlayerList contains an "Entities\Player" instance. That resolves within the project, so it
        // is not an unmapped type — reporting it would be noise.
        var glujPath = Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj");

        var result = GlueProjectLoader.Load(glujPath);

        result.Diagnostics.ShouldNotContain(d => d.Message.Contains(@"Entities\Player"));
    }
}
