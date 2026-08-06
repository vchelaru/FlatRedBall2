using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// The whole pipeline on real Glue projects: read the files, resolve the graph, build the objects.
// Beefball is the meaningful case — it is shapes-only and tile-free, so every one of its visual
// objects is a type this phase can build.
public class GlueEndToEndTests
{
    private static string Gluj(string project, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, fileName);

    [Fact]
    public void Beefball_GameScreen_BuildsWithoutErrors()
    {
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var gameScreen = result.Project.Screens.Single(s => s.Name == @"Screens\GameScreen");

        var screen = new GlueScreen { Save = gameScreen };
        screen.BuildObjects();

        result.HasErrors.ShouldBeFalse();
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void Beefball_GameScreen_BuildsTheArenaWallsInsideTheirShapeCollection()
    {
        // The walls are ContainedObjects of a ShapeCollection, which FRB2 has no equivalent for. An
        // unbuildable container must not take its buildable children down with it — otherwise the
        // one fixture chosen to demonstrate this phase renders an empty arena, silently.
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var gameScreen = result.Project.Screens.Single(s => s.Name == @"Screens\GameScreen");

        var screen = new GlueScreen { Save = gameScreen };
        screen.BuildObjects();

        var walls = Enumerable.Range(1, 6)
            .Select(i => $"Wall{i}")
            .Where(name => screen.Objects.ContainsKey(name))
            .ToList();

        walls.Count.ShouldBe(6);
        screen.Objects["Wall1"].ShouldBeOfType<AARect>();
    }

    [Fact]
    public void Beefball_PlayerBall_BuildsBothCirclesAtTheirAuthoredRadius()
    {
        // The payoff case for this phase: a real Glue entity becomes real, sized, attached FRB2
        // objects with no hand-written C# anywhere.
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var playerBall = result.Project.Entities.Single(e => e.Name == @"Entities\PlayerBall");

        var entity = new GlueEntity { X = 40f, Y = 25f, Save = playerBall };
        entity.BuildObjects();

        entity.Objects.Count.ShouldBe(2);

        var circle = (Circle)entity.Objects["CircleInstance"];
        var cooldown = (Circle)entity.Objects["CooldownCircle"];

        circle.Radius.ShouldBe(16f);
        cooldown.Radius.ShouldBe(16f);

        // Both are authored AttachToContainer, so they ride with the entity...
        circle.Parent.ShouldBe(entity);
        cooldown.Parent.ShouldBe(entity);
        circle.AbsoluteX.ShouldBe(40f);
        circle.AbsoluteY.ShouldBe(25f);

        // ...and they are visible, which FRB2 shapes are not by default.
        circle.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Beefball_Puck_BuildsItsCircle()
    {
        var result = GlueProjectLoader.Load(Gluj("Beefball", "Beefball.gluj"));
        var puck = result.Project.Entities.Single(e => e.Name == @"Entities\Puck");

        var entity = new GlueEntity { Save = puck };
        entity.BuildObjects();

        entity.Objects.Values.OfType<Circle>().ShouldNotBeEmpty();
    }

    [Fact]
    public void DoorsDemo_Player_BuildsItsSpriteAndCollisionRectangle()
    {
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));
        var player = result.Project.Entities.Single(e => e.Name == @"Entities\Player");

        var entity = new GlueEntity { Save = player };
        entity.BuildObjects();

        // The Sprite has no texture until Phase 4 — constructing it now is correct, not a failure.
        entity.Objects["SpriteInstance"].ShouldBeOfType<FlatRedBall2.Rendering.Sprite>();
        entity.Objects["AxisAlignedRectangleInstance"].ShouldBeOfType<AARect>();
        entity.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void DoorsDemo_PlayerCollisionBox_KeepsItsAuthoredOffsetAndSize()
    {
        // Regression: this rectangle is attached AND authored with an absolute Y of 11. An earlier
        // reading of Glue's semantics dropped absolute values on attached objects, which silently
        // put the player's collision box 11 units off — centred on the sprite instead of raised to
        // stand on it. Glue's codegen picks between X and RelativeX at assignment time, which is
        // what FRB2's X already means, so 11 is the offset. It is also authored Visible=false, which
        // must survive the shape-visibility default.
        // The entity's own Y is authored as a CustomVariable (-230), so the composed absolute
        // position also proves Phase 3 applied it — this test used to set Y by hand instead.
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));
        var player = result.Project.Entities.Single(e => e.Name == @"Entities\Player");

        var entity = new GlueEntity { Save = player };
        entity.BuildObjects();

        var box = (AARect)entity.Objects["AxisAlignedRectangleInstance"];

        entity.Y.ShouldBe(-230f);
        box.Y.ShouldBe(11f);
        box.AbsoluteY.ShouldBe(-219f);
        box.Width.ShouldBe(14f);
        box.Height.ShouldBe(22f);
        box.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void DoorsDemo_StartUpScreen_BootsThroughFlatRedBallServiceAndBuildsObjects()
    {
        // This is the exact boot line #804's PR description called unrunnable without "a human at a
        // keyboard": FlatRedBallService.Start<GlueScreen> needs no window or GraphicsDevice, same as
        // every other headless Screen test in this suite (see ScreenTests.RestartScreen_*).
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        var engine = new FlatRedBallService();
        engine.Start<GlueScreen>(s => s.Save = result.StartUpScreen);

        engine.CurrentScreen.ShouldBeOfType<GlueScreen>();
        var screen = (GlueScreen)engine.CurrentScreen;
        screen.GlueName.ShouldBe(@"Screens\Level1");
        screen.Objects.ShouldNotBeEmpty();
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void DoorsDemo_StillLoadsWithZeroErrors()
    {
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        result.HasErrors.ShouldBeFalse();
        result.StartUpScreen.ShouldNotBeNull();
    }
}
