using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Code-behind for loaded entities (#1411). A registered class derives from GlueEntity, receives the
// Glue data the way a plain GlueEntity does, and follows the same lifecycle contract as a
// hand-written pooled entity: CustomInitialize once per instance, Reset() on each reuse.
public class GlueEntityRegistryTests
{
    private const string HeroName = @"Entities\Hero";
    private const string PelletName = @"Entities\Pellet";

    public class Hero : GlueEntity
    {
        public int Health { get; set; }

        public int InitializeCount;
        public int DestroyCount;
        public int? HealthAtInitialize;
        public float? XAtInitialize;
        public AARect? BodyAtInitialize;

        public override void CustomInitialize()
        {
            InitializeCount++;
            HealthAtInitialize = Health;
            XAtInitialize = X;
            BodyAtInitialize = Objects.TryGetValue("Body", out var body) ? body as AARect : null;
        }

        public override void CustomDestroy() => DestroyCount++;
    }

    public class Pellet : GlueEntity
    {
        public int Damage { get; set; }
        public int InitializeCount;
        public int ResetCount;
        public int? DamageAtReset;
        public int DestroyCount;

        public override void CustomInitialize() => InitializeCount++;

        protected override void Reset()
        {
            ResetCount++;
            DamageAtReset = Damage;
        }

        public override void CustomDestroy() => DestroyCount++;
    }

    public class Enemy : GlueEntity { public int InitializeCount; public override void CustomInitialize() => InitializeCount++; }

    public class Zombie : Enemy { }

    private static string Gluj(string fixture) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", fixture, fixture + ".gluj");

    private static (FlatRedBallService Engine, GlueProject Project) Boot(
        string fixture, Action<GlueEntityRegistry> register)
    {
        var engine = new FlatRedBallService();
        register(engine.GlueEntities);
        engine.LoadGlueProject(Gluj(fixture));
        var project = engine.GlueProject!;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });
        return (engine, project);
    }

    private static (FlatRedBallService Engine, GlueProject Project) BootCodeBehind(
        Action<GlueEntityRegistry>? register = null) =>
        Boot("CodeBehind", r =>
        {
            r.Register<Hero>(HeroName).Register<Pellet>(PelletName);
            register?.Invoke(r);
        });

    [Fact]
    public void CreateEntity_ForARegisteredName_ReturnsTheRegisteredClass()
    {
        var (engine, project) = BootCodeBehind();

        var entity = project.CreateEntity(HeroName, engine.CurrentScreen);

        entity.ShouldBeOfType<Hero>();
        entity.GlueName.ShouldBe(HeroName);
    }

    [Fact]
    public void CreateEntity_ForAnUnregisteredName_StaysAPlainGlueEntity()
    {
        var (engine, project) = Boot("CodeBehind", r => r.Register<Hero>(HeroName));

        var entity = project.CreateEntity(PelletName, engine.CurrentScreen);

        entity.GetType().ShouldBe(typeof(GlueEntity));
    }

    [Fact]
    public void CreateEntity_OfARegisteredClass_BuildsItsShapesAndSprites()
    {
        var (engine, project) = BootCodeBehind();

        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        ((AARect)hero.Objects["Body"]).Width.ShouldBe(20f);
        ((Circle)hero.Objects["Aura"]).Radius.ShouldBe(7f);
        ((Sprite)hero.Objects["Art"]).TextureScale.ShouldBe(2f);
        hero.Children.OfType<AARect>().ShouldContain((AARect)hero.Objects["Body"]);
    }

    [Fact]
    public void CreateEntity_OfARegisteredClass_WritesAuthoredVariablesToItsProperties()
    {
        var (engine, project) = BootCodeBehind();

        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        hero.Health.ShouldBe(3);
    }

    // The class has no Speed property, so the authored value is held by name as for any loaded
    // entity. Not a diagnostic: Glue variables are data, and the class declares only what it uses.
    [Fact]
    public void CreateEntity_AVariableTheClassLacks_IsKeptByNameWithoutADiagnostic()
    {
        var (engine, project) = BootCodeBehind();

        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        hero.Get<float>("Speed").ShouldBe(5f);
        hero.BuildDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void SetState_OnARegisteredClass_WritesTheStatesVariablesToItsProperties()
    {
        var (engine, project) = BootCodeBehind();
        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        hero.SetState("Angry");

        hero.Health.ShouldBe(7);
    }

    [Fact]
    public void CustomInitialize_OnARegisteredClass_SeesTheBuiltObjectsAndAuthoredVariables()
    {
        var (engine, project) = BootCodeBehind();

        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        hero.InitializeCount.ShouldBe(1);
        hero.HealthAtInitialize.ShouldBe(3);
        hero.BodyAtInitialize.ShouldBeSameAs(hero.Objects["Body"]);
    }

    // The same contract Factory<T>.Create(configure) has: values the spawner supplies are in place
    // before CustomInitialize reads them.
    [Fact]
    public void ANestedInstance_OfARegisteredName_IsTheRegisteredClassWithItsOverridesApplied()
    {
        var (engine, _) = BootCodeBehind();
        var screen = (GlueScreen)engine.CurrentScreen;

        var hero = (Hero)screen.Objects["PlacedHero"];

        hero.Health.ShouldBe(9);
        hero.X.ShouldBe(50f);
        hero.InitializeCount.ShouldBe(1);
        hero.HealthAtInitialize.ShouldBe(9);
        hero.XAtInitialize.ShouldBe(50f);
    }

    [Fact]
    public void AnOverrideTheClassCannotTake_IsReportedNotThrown()
    {
        var (engine, _) = BootCodeBehind();
        var screen = (GlueScreen)engine.CurrentScreen;

        screen.BuildDiagnostics.ShouldContain(d =>
            d.Message.Contains("Ghost") && d.Message.Contains(nameof(Hero)));
    }

    [Fact]
    public void ARegisteredNameThatIsNotInTheProject_IsReportedOnceTheScreenStarts()
    {
        var (_, project) = Boot("CodeBehind", r => r.Register<Hero>(@"Entities\Wizard"));

        project.Diagnostics.Count(d => d.Message.Contains(@"Entities\Wizard")).ShouldBe(1);
    }

    [Fact]
    public void Registration_AcceptsForwardSlashesAndIgnoresCase()
    {
        var (engine, project) = Boot("CodeBehind", r => r.Register<Hero>("entities/HERO"));

        project.CreateEntity(HeroName, engine.CurrentScreen).ShouldBeOfType<Hero>();
    }

    [Fact]
    public void Registrations_BelongToTheirEngine()
    {
        var (_, withRegistration) = Boot("CodeBehind", r => r.Register<Hero>(HeroName));
        var (otherEngine, without) = Boot("CodeBehind", _ => { });

        without.CreateEntity(HeroName, otherEngine.CurrentScreen).GetType().ShouldBe(typeof(GlueEntity));
        withRegistration.Entities.IsRegistered(HeroName).ShouldBeTrue();
        without.Entities.IsRegistered(HeroName).ShouldBeFalse();
    }

    [Fact]
    public void Registrations_SurviveReloadingTheProject()
    {
        var (engine, _) = BootCodeBehind();
        engine.GlueProject = null;
        engine.GlueEntities.IsRegistered(HeroName).ShouldBeTrue();

        engine.LoadGlueProject(Gluj("CodeBehind"));

        engine.GlueProject!.Entities.ShouldBeSameAs(engine.GlueEntities);
    }

    [Fact]
    public void AProjectLoadedWithoutAnEngine_HasItsOwnRegistry()
    {
        var first = GlueProject.Load(Gluj("CodeBehind"));
        var second = GlueProject.Load(Gluj("CodeBehind"));

        first.Entities.Register<Hero>(HeroName);

        second.Entities.IsRegistered(HeroName).ShouldBeFalse();
    }

    // Pooling. A pooled registered instance keeps what CustomInitialize set up, so it is reused as
    // it is: CustomInitialize does not run again and Reset() is the per-life hook.
    [Fact]
    public void PooledRegisteredEntity_WhenReused_RunsResetNotASecondCustomInitialize()
    {
        var (engine, project) = BootCodeBehind();

        var first = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);
        first.ResetCount.ShouldBe(0);
        first.Destroy();
        var second = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);

        second.ShouldBeSameAs(first);
        second.InitializeCount.ShouldBe(1);
        second.ResetCount.ShouldBe(1);
    }

    [Fact]
    public void PooledRegisteredEntity_WhenReused_KeepsItsBuiltObjects()
    {
        var (engine, project) = BootCodeBehind();
        var first = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);
        var body = first.Objects["Body"];
        first.Destroy();

        var second = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);

        second.Objects["Body"].ShouldBeSameAs(body);
        second.Children.ShouldContain((AARect)body);
    }

    [Fact]
    public void PooledRegisteredEntity_WhenReused_RestoresAuthoredValues()
    {
        var (engine, project) = BootCodeBehind();
        var pellet = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);
        pellet.Damage = 50;
        ((AARect)pellet.Objects["Body"]).Width = 99f;
        pellet.X = 300f;
        pellet.Destroy();

        var reused = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);

        reused.Damage.ShouldBe(2);
        reused.DamageAtReset.ShouldBe(2);
        ((AARect)reused.Objects["Body"]).Width.ShouldBe(4f);
        reused.X.ShouldBe(0f);
    }

    [Fact]
    public void PooledRegisteredEntity_WhenDestroyed_SkipsCustomDestroyAndLeavesTheScreen()
    {
        var (engine, project) = BootCodeBehind();
        var pellet = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);

        pellet.Destroy();

        pellet.DestroyCount.ShouldBe(0);
        project.InstancesOf(PelletName).ShouldNotContain(pellet);
        engine.CurrentScreen.Entities.ShouldNotContain(pellet);
    }

    [Fact]
    public void PooledRegisteredEntity_WhenReused_IsBackOnTheScreenOnce()
    {
        var (engine, project) = BootCodeBehind();
        var pellet = (Pellet)project.CreateEntity(PelletName, engine.CurrentScreen);
        pellet.Destroy();

        project.CreateEntity(PelletName, engine.CurrentScreen);

        engine.CurrentScreen.Entities.Count(e => e == pellet).ShouldBe(1);
        project.InstancesOf(PelletName).Count(e => e == pellet).ShouldBe(1);
    }

    [Fact]
    public void NonPooledRegisteredEntity_WhenDestroyed_RunsCustomDestroyAndLeavesTheScreen()
    {
        var (engine, project) = BootCodeBehind();
        var hero = (Hero)project.CreateEntity(HeroName, engine.CurrentScreen);

        hero.Destroy();

        hero.DestroyCount.ShouldBe(1);
        engine.CurrentScreen.Entities.ShouldNotContain(hero);
    }

    // Derived entities inherit their base's code-behind, as FRB1's derived classes do, so a
    // base-typed list holds only objects of the base class.
    [Fact]
    public void DerivedEntity_WithoutItsOwnRegistration_UsesTheNearestRegisteredAncestor()
    {
        var (engine, project) = Boot("BaseTypedLists", r => r.Register<Enemy>(@"Entities\Enemy"));

        var boss = project.CreateEntity(@"Entities\Boss", engine.CurrentScreen);

        boss.ShouldBeOfType<Enemy>();
        boss.GlueName.ShouldBe(@"Entities\Boss");
    }

    [Fact]
    public void DerivedEntity_WithItsOwnRegistration_UsesItAndStillJoinsTheBaseTypedList()
    {
        var (engine, project) = Boot("BaseTypedLists", r =>
            r.Register<Enemy>(@"Entities\Enemy").Register<Zombie>(@"Entities\Zombie"));

        var zombie = project.CreateEntity(@"Entities\Zombie", engine.CurrentScreen);

        zombie.ShouldBeOfType<Zombie>();
        ((GlueScreen)engine.CurrentScreen).GetList("EnemyList").ShouldContain(zombie);
    }

    // Lists are typed by Glue names, not classes: an Enemy list holds Zombie instances as GlueEntity,
    // and code casting them to the base class must always succeed.
    [Fact]
    public void BaseTypedList_HoldingDerivedSpawns_OnlyHoldsTheBaseClass()
    {
        var (engine, project) = Boot("BaseTypedLists", r => r.Register<Enemy>(@"Entities\Enemy"));

        project.CreateEntity(@"Entities\Zombie", engine.CurrentScreen);
        project.CreateEntity(@"Entities\Enemy", engine.CurrentScreen);

        ((GlueScreen)engine.CurrentScreen).GetList("EnemyList").ShouldAllBe(e => e is Enemy);
    }
}
