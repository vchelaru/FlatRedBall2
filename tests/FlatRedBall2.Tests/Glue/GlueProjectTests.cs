using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers the project context: the thing that knows every loaded element by name, so a screen can
// spawn a nested entity and game code can navigate without a compile-time type.
public class GlueProjectTests
{
    private static string Gluj(string project) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, project + ".gluj");

    private static GlueProject Load(string project) =>
        GlueProject.Load(Gluj(project));

    [Fact]
    public void FindEntity_ByGlueName_ResolvesRegardlessOfCase()
    {
        // Glue authored these names on Windows. A case-sensitive miss is a Linux-and-web-only
        // failure, which is the worst kind to ship.
        var project = Load("Beefball");

        project.FindEntity(@"Entities\PlayerBall").ShouldNotBeNull();
        project.FindEntity(@"entities\playerball").ShouldNotBeNull();
        project.FindEntity(@"Entities\Nope").ShouldBeNull();
    }

    [Fact]
    public void FindScreen_ResolvesTheStartUpScreen()
    {
        var project = Load("DoorsDemo");

        project.StartUpScreen.ShouldNotBeNull();
        project.StartUpScreen!.Name.ShouldBe(@"Screens\Level1");
        project.FindScreen(@"Screens\GameScreen").ShouldNotBeNull();
    }

    [Fact]
    public void CreateEntity_UnknownName_ThrowsNamingTheAcceptedForm()
    {
        // The generic API is compile-checked; a string one is not, so a miss has to be loud and the
        // message has to show what a correct name looks like.
        var project = Load("Beefball");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        var error = Should.Throw<ArgumentException>(() => project.CreateEntity("PlayerBall", screen));

        error.Message.ShouldContain(@"Entities\PlayerBall");
    }

    [Fact]
    public void CreateEntity_SeparatorStyle_IsAccepted()
    {
        // A user typing a forward slash is the likeliest mistake, and it costs one Replace to accept.
        var project = Load("Beefball");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        var entity = project.CreateEntity("Entities/PlayerBall", screen);

        entity.GlueName.ShouldBe(@"Entities\PlayerBall");
    }

    [Fact]
    public void CreateEntity_BuildsTheEntitysOwnObjectsAndTracksIt()
    {
        var project = Load("Beefball");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        var entity = project.CreateEntity(@"Entities\PlayerBall", screen);

        entity.Objects.ShouldContainKey("CircleInstance");
        project.InstancesOf(@"Entities\PlayerBall").ShouldContain(entity);
    }

    [Fact]
    public void CreateEntity_AbstractElement_Throws()
    {
        // An element that leaves an object for a derived element to supply is missing its own
        // content by construction. Every other failure here degrades; this one must not.
        var project = Load("DoorsDemo");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        // GameScreen is abstract, but so is any element with a SetByDerived object; assert on the
        // screen-side guard since DoorsDemo has no abstract entity.
        project.FindScreen(@"Screens\GameScreen")!.IsAbstract.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(
            () => project.CreateScreen(@"Screens\GameScreen"));
    }

    [Fact]
    public void BuildObjects_ScreenWithANestedEntityList_InstantiatesItsMembers()
    {
        // Beefball's GameScreen holds a PlayerBallList whose contained object is an
        // Entities\PlayerBall instance. Until a project context existed, that was unbuildable.
        var project = Load("Beefball");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        screen.BuildObjects();

        var list = screen.Objects["PlayerBallList"].ShouldBeOfType<System.Collections.Generic.List<object>>();

        list.ShouldNotBeEmpty();
        list[0].ShouldBeOfType<GlueEntity>();
        ((GlueEntity)list[0]).GlueName.ShouldBe(@"Entities\PlayerBall");
    }

    [Fact]
    public void BuildObjects_NestedEntity_NoLongerReportsAsUnbuildable()
    {
        var project = Load("Beefball");
        var screen = new GlueScreen { Project = project, Save = project.StartUpScreen };

        screen.BuildObjects();

        screen.BuildDiagnostics.ShouldNotContain(d => d.Message.Contains(@"Entities\PlayerBall"));
    }
}
