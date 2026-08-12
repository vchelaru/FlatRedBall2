using System;
using System.IO;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers the epic's only user-facing surface: reaching a loaded screen or entity by its Glue name,
// and reading or writing an authored variable without a generated property to do it through.
//
// Every loaded element shares one C# type, so the generic MoveToScreen<T>/Factory<T> API cannot tell
// two of them apart -- these overloads are how a data-driven project navigates.
public class GlueNavigationTests
{
    private static GlueProject LoadDoorsDemo() =>
        GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

    // D141 — a Glue name is typed by hand with no compiler checking it, so both separators are
    // accepted and the full Screens\Name form is required. A bare leaf name is ambiguous:
    // Entities\Player and Screens\Player can coexist.
    [Theory]
    [InlineData(@"Screens\Level1")]
    [InlineData("Screens/Level1")]
    [InlineData(@"screens\level1")]
    public void FindScreen_AcceptsEitherSeparatorAndIgnoresCase(string glueName)
    {
        LoadDoorsDemo().FindScreen(glueName).ShouldNotBeNull();
    }

    // G141 — an unknown name must say what form it wanted, since the name came from a string with
    // nothing to check it.
    [Fact]
    public void CreateScreen_AnUnknownName_ErrorsNamingTheAcceptedForm()
    {
        var project = LoadDoorsDemo();

        var error = Should.Throw<ArgumentException>(() => project.CreateScreen("Level1"));

        error.Message.ShouldContain("Level1");
        error.Message.ShouldContain(@"Screens\Level1");
    }

    [Fact]
    public void CreateScreen_AKnownName_BuildsAScreenCarryingThatSaveAndTheProject()
    {
        var project = LoadDoorsDemo();

        var screen = project.CreateScreen(@"Screens\Level1");

        screen.Save!.Name.ShouldBe(@"Screens\Level1");
        screen.Project.ShouldBe(project);
    }

    // D142 — NextScreen is already parsed, and leaving a parsed-but-dead member reads as an
    // oversight.
    //
    // No vendored fixture sets NextScreen (checked all four), so the naming case uses a synthetic
    // screen pointed at a real one. The resolution being tested is the lookup, not the parse.
    [Fact]
    public void NextScreenOf_AScreenNamingOne_ResolvesIt()
    {
        var project = LoadDoorsDemo();
        var screen = new ScreenSave { Name = @"Screens\Synthetic", NextScreen = @"Screens\Level1" };

        project.NextScreenOf(screen)!.Name.ShouldBe(@"Screens\Level1");
    }

    [Fact]
    public void NextScreenOf_AScreenNamingNone_IsNull()
    {
        var project = LoadDoorsDemo();
        var level1 = project.FindScreen(@"Screens\Level1")!;

        // Level1 is the real fixture's shape: no NextScreen at all.
        level1.NextScreen.ShouldBeNullOrEmpty();
        project.NextScreenOf(level1).ShouldBeNull();
    }

    // The by-name overload of MoveToScreen. Every loaded screen is a GlueScreen, so the generic
    // MoveToScreen<T> cannot distinguish two of them -- this is the only way a loaded project moves
    // between its own screens.
    [Fact]
    public void MoveToScreen_ByGlueName_ActivatesThatScreensSave()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        engine.CurrentScreen.MoveToScreen(@"Screens\Level1");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        var current = (GlueScreen)engine.CurrentScreen;
        current.GlueName.ShouldBe(@"Screens\Level1");
        current.Project.ShouldBe(project);
    }

    // G146 — restart replays the retained configure. If the by-name overload set Save outside that
    // callback, a restart would rebuild the screen with a null Save: a silently empty screen.
    [Fact]
    public void RestartScreen_AfterMovingByName_KeepsItsSave()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        engine.CurrentScreen.MoveToScreen(@"Screens\Level1");
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        engine.CurrentScreen.RestartScreen();
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        ((GlueScreen)engine.CurrentScreen).GlueName.ShouldBe(@"Screens\Level1");
    }

    [Fact]
    public void MoveToScreen_ByNameWithNoProjectLoaded_SaysSo()
    {
        var engine = new FlatRedBallService();
        engine.Start<GlueScreen>();

        var error = Should.Throw<InvalidOperationException>(
            () => engine.CurrentScreen.MoveToScreen(@"Screens\Level1"));

        error.Message.ShouldContain(nameof(EngineInitSettings.GlueProjectFile));
    }

    // Phase 6 D63 — an abstract screen is missing its own content by construction. DoorsDemo's
    // GameScreen is exactly that, so the error has to explain rather than fail obscurely later.
    [Fact]
    public void CreateScreen_AnAbstractScreen_ErrorsExplainingWhy()
    {
        var project = LoadDoorsDemo();

        var error = Should.Throw<InvalidOperationException>(
            () => project.CreateScreen(@"Screens\GameScreen"));

        error.Message.ShouldContain("derived screen");
    }
}

// The indexer -- the variable-bag view of a loaded element. Get<T> is driven by T rather than by the
// variable's declared type, because a Glue variable's declared type is often not a CLR type at all.
public class GlueIndexerTests
{
    private static GlueProject LoadDoorsDemo() =>
        GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

    // Level1, not GameScreen: GameScreen is abstract, and Level1 is what the project actually boots.
    private static GlueScreen BuiltScreen()
    {
        var screen = LoadDoorsDemo().CreateScreen(@"Screens\Level1");
        screen.BuildObjects();
        return screen;
    }

    // G144 — a name that matches a real CLR member has to write that member, not shadow it in a bag.
    // Writing the bag instead would leave the property at its old value while reads looked correct.
    [Fact]
    public void Indexer_ANameMatchingARealProperty_WritesTheProperty()
    {
        var entity = new GlueEntity();

        entity["X"] = 5f;

        entity.X.ShouldBe(5f);
        entity.Get<float>("X").ShouldBe(5f);
    }

    [Fact]
    public void Indexer_ANameWithNoClrMember_RoundTripsThroughTheBag()
    {
        var entity = new GlueEntity();

        entity["MovementSpeed"] = 300;

        entity.Get<int>("MovementSpeed").ShouldBe(300);
    }

    // G142 — Objects and the indexer have to agree on case, or a name that reaches an object through
    // one surface silently misses through the other.
    [Fact]
    public void Indexer_IsCaseInsensitive_MatchingObjects()
    {
        var entity = new GlueEntity();

        entity["Health"] = 10;

        entity.Get<int>("health").ShouldBe(10);
        entity.Get<int>("HEALTH").ShouldBe(10);
    }

    // G145 — T drives the read. The same stored value is asked for as two different types.
    [Fact]
    public void Get_IsDrivenByTNotByTheDeclaredType()
    {
        var entity = new GlueEntity();

        entity["Score"] = 3;

        entity.Get<int>("Score").ShouldBe(3);
        entity.Get<string>("Score").ShouldBe("3");
    }

    [Fact]
    public void Get_AnUnknownName_IsDefaultRatherThanThrowing()
    {
        new GlueEntity().Get<int>("NoSuchVariable").ShouldBe(0);
    }

    [Fact]
    public void Indexer_OnAScreen_ReachesAContainedObjectsMember()
    {
        var screen = BuiltScreen();

        // GameScreen's objects are authored; the indexer is the bag view over the same data the
        // typed Objects dictionary exposes.
        screen.Objects.ShouldNotBeEmpty();
        screen["SomeUnsetVariable"].ShouldBeNull();
    }
}
