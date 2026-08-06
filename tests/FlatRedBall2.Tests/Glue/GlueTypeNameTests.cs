using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Glue's SourceClassType is not a flat set of CLR type names. Every string here was taken verbatim
// from the DoorsDemo fixture, and each shape breaks naive whole-string matching in a different way.
public class GlueTypeNameTests
{
    [Fact]
    public void Parse_ClosedGenericWithElementArguments_ExposesArgumentsAsElementCandidates()
    {
        // Glue writes the same entity two ways depending on position: "Entities\Player" standing
        // alone, but "Entities.Player" as a generic argument, because there it is the generated C#
        // class name. Both must reduce to the one element name the project knows.
        var parsed = GlueTypeName.Parse(
            "FlatRedBall.Math.Collision.ListVsListRelationship<Entities.Player, Entities.Door>");

        parsed.OpenTypeName.ShouldBe("FlatRedBall.Math.Collision.ListVsListRelationship");
        parsed.TypeArguments.Count.ShouldBe(2);
        parsed.TypeArguments[0].ToElementNameCandidate().ShouldBe(@"Entities\Player");
        parsed.TypeArguments[1].ToElementNameCandidate().ShouldBe(@"Entities\Door");
    }

    [Fact]
    public void ToElementNameCandidate_EngineTypeInAnEntitiesNamespace_DoesNotMasqueradeAsAnElement()
    {
        // FlatRedBall.Entities.CameraControllingEntity is an engine type whose namespace happens to
        // contain "Entities". Any prefix-matching heuristic would misread it as a game entity; the
        // candidate it produces simply matches no element, which is the correct outcome.
        var parsed = GlueTypeName.Parse("FlatRedBall.Entities.CameraControllingEntity");

        parsed.ToElementNameCandidate().ShouldBe(@"FlatRedBall\Entities\CameraControllingEntity");
    }

    [Fact]
    public void Parse_ElementReferenceInBackslashForm_ClassifiesAsElement()
    {
        // A nested entity puts an element name where a type name would go.
        var parsed = GlueTypeName.Parse(@"Entities\Player");

        parsed.IsElementReference.ShouldBeTrue();
        parsed.ElementName.ShouldBe(@"Entities\Player");
        parsed.TypeArguments.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_NestedGeneric_KeepsInnerArgumentIntact()
    {
        // Splitting on commas naively would tear this argument in half.
        var parsed = GlueTypeName.Parse("Outer<Inner<A, B>, C>");

        parsed.OpenTypeName.ShouldBe("Outer");
        parsed.TypeArguments.Count.ShouldBe(2);
        parsed.TypeArguments[0].OpenTypeName.ShouldBe("Inner");
        parsed.TypeArguments[0].TypeArguments.Count.ShouldBe(2);
        parsed.TypeArguments[1].OpenTypeName.ShouldBe("C");
    }

    [Fact]
    public void Parse_PlainTypeName_HasNoArgumentsAndIsNotAnElement()
    {
        var parsed = GlueTypeName.Parse("FlatRedBall.Sprite");

        parsed.OpenTypeName.ShouldBe("FlatRedBall.Sprite");
        parsed.TypeArguments.ShouldBeEmpty();
        parsed.IsElementReference.ShouldBeFalse();
        parsed.IsUnresolvedGeneric.ShouldBeFalse();
    }

    [Fact]
    public void Parse_UnresolvedGenericPlaceholder_IsFlaggedNotTreatedAsAType()
    {
        // Lists are written with a literal "<T>" rather than a concrete argument.
        var parsed = GlueTypeName.Parse("FlatRedBall.Math.PositionedObjectList<T>");

        parsed.OpenTypeName.ShouldBe("FlatRedBall.Math.PositionedObjectList");
        parsed.IsUnresolvedGeneric.ShouldBeTrue();
        parsed.TypeArguments.Count.ShouldBe(1);
        parsed.TypeArguments[0].OpenTypeName.ShouldBe("T");
    }

    [Fact]
    public void Parse_WhitespaceAroundArguments_IsTrimmed()
    {
        var parsed = GlueTypeName.Parse("Pair<  A ,B  >");

        parsed.TypeArguments[0].OpenTypeName.ShouldBe("A");
        parsed.TypeArguments[1].OpenTypeName.ShouldBe("B");
    }
}
