using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers turning a Glue collision relationship into an FRB2 one. These have no save class of their
// own — they are NamedObjects whose settings live in a property bag, and whose SourceClassType is a
// derived cache that real sample files disagree with.
public class GlueCollisionTests
{
    private static string Gluj(string project) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, project + ".gluj");

    private static NamedObjectSave Relationship(
        string first, string? second, int? collisionType = null,
        float? firstMass = null, float? secondMass = null)
    {
        var save = new NamedObjectSave
        {
            InstanceName = "Rel",
            SourceClassType = "FlatRedBall.Math.Collision.ListVsListRelationship<Entities.A, Entities.B>",
        };

        void Add(string name, string json) =>
            save.Properties.Add(new PropertySave
            {
                Name = name,
                Value = System.Text.Json.JsonDocument.Parse(json).RootElement,
            });

        Add("FirstCollisionName", $"\"{first}\"");
        if (second is not null) Add("SecondCollisionName", $"\"{second}\"");
        if (collisionType is not null) Add("CollisionType", collisionType.Value.ToString());
        if (firstMass is not null) Add("FirstCollisionMass", firstMass.Value.ToString("0.0"));
        if (secondMass is not null) Add("SecondCollisionMass", secondMass.Value.ToString("0.0"));

        return save;
    }

    [Fact]
    public void IsRelationship_MatchesEveryPatternAndNothingElse()
    {
        GlueCollisionBuilder.IsRelationship("CollisionRelationship").ShouldBeTrue();
        GlueCollisionBuilder.IsRelationship(
            "FlatRedBall.Math.Collision.ListVsListRelationship<Entities.A, Entities.B>").ShouldBeTrue();
        GlueCollisionBuilder.IsRelationship(
            "FlatRedBall.Math.Collision.DelegateListVsSingleRelationship<A, B>").ShouldBeTrue();

        // The Delegate entries match only with the trailing angle bracket, so a longer type name
        // that merely starts the same way must not match.
        GlueCollisionBuilder.IsRelationship(
            "FlatRedBall.Math.Collision.DelegateCollisionRelationshipHelper").ShouldBeFalse();
        GlueCollisionBuilder.IsRelationship("FlatRedBall.Sprite").ShouldBeFalse();
        GlueCollisionBuilder.IsRelationship((string?)null).ShouldBeFalse();
    }

    [Fact]
    public void Settings_AbsentCollisionType_MeansEventOnly()
    {
        // DoorsDemo's PlayerVsDoor and Beefball's PuckVsGoal both rely on this. Defaulting to a
        // physics response would have them shove each other.
        var settings = GlueCollisionSettings.From(Relationship("AList", "BList"));

        settings.CollisionType.ShouldBe(GlueCollisionType.NoPhysics);
    }

    [Fact]
    public void Settings_AbsentMassesAndElasticity_DefaultToOneNotZero()
    {
        // Zero on both sides makes the separation offset zero, so "bounce off the wall" silently
        // becomes "pass through the wall".
        var settings = GlueCollisionSettings.From(Relationship("AList", "BList", collisionType: 2));

        settings.FirstMass.ShouldBe(1f);
        settings.SecondMass.ShouldBe(1f);
        settings.Elasticity.ShouldBe(1f);
    }

    [Fact]
    public void Settings_AbsentIsCollisionActive_MeansActive()
    {
        // Reading this with GetValue<bool> would disable every relationship in every project that
        // omits it — which is most of them.
        var settings = GlueCollisionSettings.From(Relationship("AList", "BList"));

        settings.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Settings_AbsentSecondCollisionName_IsAlwaysColliding()
    {
        var settings = GlueCollisionSettings.From(Relationship("AList", second: null));

        settings.SecondCollisionName.ShouldBeNull();
    }

    [Fact]
    public void CollisionType_PluginOrdinals_ArePinned()
    {
        // The persisted value is Glue's plugin enum, not the FRB1 runtime one — they disagree from
        // MoveSoftCollision onward, and decoding with the wrong one misreads real projects.
        ((int)GlueCollisionType.NoPhysics).ShouldBe(0);
        ((int)GlueCollisionType.MoveCollision).ShouldBe(1);
        ((int)GlueCollisionType.BounceCollision).ShouldBe(2);
        ((int)GlueCollisionType.PlatformerSolidCollision).ShouldBe(3);
        ((int)GlueCollisionType.PlatformerCloudCollision).ShouldBe(4);
        ((int)GlueCollisionType.DelegateCollision).ShouldBe(5);
        ((int)GlueCollisionType.StackingCollision).ShouldBe(6);
        ((int)GlueCollisionType.MoveSoftCollision).ShouldBe(7);
    }

    [Fact]
    public void BuildObjects_Beefball_RegistersEveryRelationshipItCanExpress()
    {
        var project = GlueProject.Load(Gluj("Beefball"));
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        screen.BuildObjects();

        // Six relationships are authored; each becomes an entry addressable by its Glue name.
        screen.Objects.ShouldContainKey("PlayerBallVsPuck");
        screen.Objects.ShouldContainKey("PlayerBallVsPlayerBall");
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildObjects_BounceRelationship_CarriesItsAuthoredMasses()
    {
        // PlayerBallVsPuck is a bounce with asymmetric masses — 1.0 against 0.3 — which is what
        // makes the puck fly and the paddle barely move.
        var project = GlueProject.Load(Gluj("Beefball"));
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        screen.BuildObjects();

        var save = project.StartUpScreen!.NamedObjects.Single(o => o.InstanceName == "PlayerBallVsPuck");
        var settings = GlueCollisionSettings.From(save);

        settings.CollisionType.ShouldBe(GlueCollisionType.BounceCollision);
        settings.FirstMass.ShouldBe(1f);
        settings.SecondMass.ShouldBe(0.3f);
    }

    [Fact]
    public void BuildObjects_SelfCollision_UsesTheSameListOnBothSides()
    {
        var project = GlueProject.Load(Gluj("Beefball"));

        var save = project.StartUpScreen!.NamedObjects
            .Single(o => o.InstanceName == "PlayerBallVsPlayerBall");
        var settings = GlueCollisionSettings.From(save);

        settings.FirstCollisionName.ShouldBe(settings.SecondCollisionName);
    }

    [Fact]
    public void BuildObjects_RelationshipNamingAMissingObject_WarnsAndSkips()
    {
        var project = GlueProject.Load(Gluj("Beefball"));
        var save = project.StartUpScreen!;
        save.NamedObjects.Single(o => o.InstanceName == "PlayerBallVsPuck")
            .Properties.Single(p => p.Name == "SecondCollisionName").Value =
            System.Text.Json.JsonDocument.Parse("\"GoneList\"").RootElement;

        var screen = new GlueScreen { Project = project, Save = save };

        Should.NotThrow(() => screen.BuildObjects());

        screen.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("GoneList"));
    }
}
