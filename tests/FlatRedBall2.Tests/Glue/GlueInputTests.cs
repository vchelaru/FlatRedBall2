using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers the last hop of Phases 11 and 12: an entity whose authored physics are loaded still needs
// something telling it to move. Glue records that as a single InputDevice choice per entity.
public class GlueInputTests
{
    // With a content source: without one the entity's CSVs never load, and every movement slot
    // reads null for a reason that has nothing to do with what is under test.
    private static GlueProject LoadDoorsDemo() =>
        GlueProject.Load(
            Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"),
            new GlueContentSource(new ContentLoader(), Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content")));

    private static EntitySave EntityWithInputDevice(int inputDevice)
    {
        var save = new EntitySave { Name = @"Entities\Test" };
        save.Properties.Add(new PropertySave
        {
            Name = "InputDevice",
            Value = System.Text.Json.JsonDocument.Parse(inputDevice.ToString()).RootElement.Clone(),
        });
        return save;
    }

    // Glue's enum, from EntityInputMovementPlugin's MainViewModel: the default is 0, and it is not
    // "none" -- an entity that says nothing still gets input.
    [Fact]
    public void InputDevice_TheAbsentDefault_IsGamepadWithKeyboardFallback()
    {
        new EntitySave().InputDevice.ShouldBe((int)GlueInputDevice.GamepadWithKeyboardFallback);
        ((int)GlueInputDevice.GamepadWithKeyboardFallback).ShouldBe(0);
        ((int)GlueInputDevice.None).ShouldBe(1);
        ((int)GlueInputDevice.ZeroInputDevice).ShouldBe(2);
    }

    [Fact]
    public void Bind_GamepadWithKeyboardFallback_ProducesMovementAndJumpInput()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.GamepadWithKeyboardFallback), engine.Input);

        bound.MovementInput.ShouldNotBeNull();
        bound.JumpInput.ShouldNotBeNull();
    }

    // FRB1's keyboard fallback is Keyboard.Default2DInput, which is WASD (Keyboard.cs:110) — the
    // generated InitializeInput binds nothing else. Arrows are additive: FRB1 gives them no default
    // meaning, so binding both keeps parity and costs an FRB1 project nothing.
    [Theory]
    [InlineData(Keys.D, 1f, 0f)]
    [InlineData(Keys.W, 0f, 1f)]
    [InlineData(Keys.Right, 1f, 0f)]
    [InlineData(Keys.Up, 0f, 1f)]
    public void Bind_GamepadWithKeyboardFallback_ReadsWasdAndArrows(Keys held, float x, float y)
    {
        var engine = new FlatRedBallService();
        engine.Input.InjectKey(held, down: true);

        // Injected keys reach IsKeyDown only after the per-frame capture.
        engine.Input.Update(TimeSpan.Zero);

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.GamepadWithKeyboardFallback), engine.Input);

        bound.MovementInput!.X.ShouldBe(x);
        bound.MovementInput.Y.ShouldBe(y);
    }

    // "None" means the game wires its own input -- Glue generates nothing at all for it, so binding
    // anything would override a choice the author made deliberately.
    [Fact]
    public void Bind_None_LeavesInputUnbound()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.None), engine.Input);

        bound.MovementInput.ShouldBeNull();
        bound.JumpInput.ShouldBeNull();
    }

    // ZeroInputDevice is not the same as None: it binds a real input that always reads zero, which
    // is how an author pins an entity still without leaving it open to a later binding.
    [Fact]
    public void Bind_ZeroInputDevice_BindsAnInputThatAlwaysReadsZero()
    {
        var engine = new FlatRedBallService();

        var bound = GlueInputBinder.Bind(
            EntityWithInputDevice((int)GlueInputDevice.ZeroInputDevice), engine.Input);

        bound.MovementInput.ShouldNotBeNull();
        bound.MovementInput!.X.ShouldBe(0f);
        bound.MovementInput.Y.ShouldBe(0f);
        bound.JumpInput!.IsDown.ShouldBeFalse();
    }

    [Fact]
    public void BuildObjects_APlatformerEntity_ReceivesItsInput()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        // The Player's authored physics reached the behaviour in Phase 12; this is what finally
        // gives it a way to be told to move.
        player.Platformer.MovementInput.ShouldNotBeNull();
        player.Platformer.JumpInput.ShouldNotBeNull();
    }

    // Phase 11's deferred pieces: the values have to reach TopDownBehavior, and Glue's generated code
    // defaults to the first CSV row rather than leaving the entity with no values at all.
    //
    // The name lives in the dictionary key, not on TopDownValues -- which is why the reader returns
    // a dictionary and the first-row default is the first *entry*.
    private static Dictionary<string, TopDownValues> TwoMovements() => new()
    {
        ["Default"] = new TopDownValues { MaxSpeed = 100f },
        ["Fast"] = new TopDownValues { MaxSpeed = 300f },
    };

    [Fact]
    public void TopDown_AnEntityWithMovementValues_UsesTheFirstRowByDefault()
    {
        var entity = new GlueEntity();

        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.TopDown.MovementValues.ShouldNotBeNull();
        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(100f);
    }

    [Fact]
    public void TopDown_NamedValues_CanBeSelectedByName()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("Fast");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(300f);
    }

    // A data-driven name has no compiler checking it, and a typo that stopped the entity dead would
    // read as a movement bug rather than a bad string.
    [Fact]
    public void TopDown_AnUnknownMovementName_LeavesTheCurrentValuesAlone()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("NoSuchMovement");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(100f);
    }

    // The end-to-end path, on a real FRB1 top-down entity read from disk. Everything above tests the
    // mapping in isolation; this is what proves the loader ever calls it. It did not: ReadTopDown
    // and ApplyTopDown had no production call site at all, so a real top-down entity loaded with no
    // movement values and nothing said so.
    private static GlueProject LoadTopDownProject() =>
        GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "TopDownProject", "TopDownProject.gluj"),
            new GlueContentSource(
                new ContentLoader(), Path.Combine("Glue", "Fixtures", "TopDownProject", "Content")));

    [Fact]
    public void BuildObjects_ATopDownEntity_LoadsItsMovementValuesFromItsCsv()
    {
        var engine = new FlatRedBallService();
        var project = LoadTopDownProject();
        engine.GlueProject = project;
        engine.Start<GlueScreen>();

        var entity = project.CreateEntity(@"Entities\TopDownMovementEntity", engine.CurrentScreen);

        // "Default" is the CSV's first row, at MaxSpeed 76.
        entity.TopDown.MovementValues.ShouldNotBeNull();
        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(76f);
    }

    [Fact]
    public void BuildObjects_ATopDownEntity_CanSelectAnotherAuthoredRow()
    {
        var engine = new FlatRedBallService();
        var project = LoadTopDownProject();
        engine.GlueProject = project;
        engine.Start<GlueScreen>();

        var entity = project.CreateEntity(@"Entities\TopDownMovementEntity", engine.CurrentScreen);
        entity.SetTopDownMovement("OverrideMe");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(10f);
    }

    // Loading the values and binding the input still leaves the behaviour untouched until something
    // ticks it every frame. FRB1 generated that call into the entity's Activity; a loaded entity has
    // no generated class, so GlueEntity has to make it itself.
    private sealed class HeldAxisInput(float x, float y) : I2DInput
    {
        public float X => x;
        public float Y => y;
    }

    private static FrameTime Frame() => new(
        TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void CustomActivity_ATopDownEntity_MovesWhileInputIsHeld()
    {
        var engine = new FlatRedBallService();
        var project = LoadTopDownProject();
        engine.GlueProject = project;
        engine.Start<GlueScreen>();

        var entity = project.CreateEntity(@"Entities\TopDownMovementEntity", engine.CurrentScreen);
        entity.TopDown.MovementInput = new HeldAxisInput(1f, 0f);

        // Two frames: the first is the CustomActivity that sets acceleration, the second is the
        // physics pass that turns it into movement.
        engine.CurrentScreen.Update(Frame());
        engine.CurrentScreen.Update(Frame());

        entity.X.ShouldBeGreaterThan(0f);
    }

    // Gravity rather than input, because it needs no ground contact to observe: the airborne slot's
    // Gravity is applied by Platformer.Update and by nothing else.
    [Fact]
    public void CustomActivity_APlatformerEntity_FallsUnderItsAuthoredGravity()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        engine.CurrentScreen.Update(Frame());

        player.AccelerationY.ShouldBeLessThan(0f);
    }

    // G114 — FRB1's generated Initialize hard-sets PossibleDirections = FourWay unconditionally,
    // ignoring the CSV, and Glue persists no per-entity direction setting at all. FRB2 defaults to
    // EightWay, so a loaded entity that kept that default would snap diagonally where FRB1 does not.
    [Fact]
    public void TopDown_ALoadedEntity_SnapsFourWayMatchingGluesForcedDefault()
    {
        new GlueEntity().TopDown.DirectionSnap.ShouldBe(DirectionSnap.FourWay);

        // The engine's own default is unchanged for hand-written entities.
        new TopDownBehavior().DirectionSnap.ShouldBe(DirectionSnap.EightWay);
    }

    [Fact]
    public void TopDown_ANonTopDownEntity_GetsNoMovementValues()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        // Player is a platformer; IsTopDown is absent, so nothing top-down is loaded for it.
        player.TopDown.MovementValues.ShouldBeNull();
    }

    // Climbing. FRB1 has no climbing slot -- CanClimb is a per-row bool, and its generated code
    // expects game code to swap that row into the ground slot while on a ladder. FRB2 owns the
    // ladder state itself and reads ClimbingMovement whenever IsClimbing, so the row just fills it.
    // DoorsDemo's Player CSV has exactly one CanClimb=True row: "Climbing", MaxClimbingSpeed 75.
    [Fact]
    public void BuildObjects_APlatformerWithAClimbingRow_FillsTheClimbingSlot()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        player.Platformer.ClimbingMovement.ShouldNotBeNull();
        player.Platformer.ClimbingMovement!.ClimbingSpeed.ShouldBe(75f);
    }

    // The slot is read only while IsClimbing, and the behaviour throws if it climbs without one --
    // so filling it eagerly is safe, and an entity that never climbs is unaffected.
    [Fact]
    public void BuildObjects_APlatformerWithAClimbingRow_DoesNotStartOutClimbing()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

        var player = project.CreateEntity(@"Entities\Player", engine.CurrentScreen);

        player.Platformer.IsClimbing.ShouldBeFalse();
        player.Platformer.GroundMovement.ShouldNotBeNull();
    }

    [Fact]
    public void FindClimbingRow_ACsvWithNoClimbingRow_IsNull()
    {
        const string csv = """
            "Name (string, required)",CanClimb (System.Boolean),MaxClimbingSpeed (System.Single)
            Ground,False,0
            Air,False,0
            """;

        GlueMovementValues.FindClimbingRow(csv).ShouldBeNull();
    }

    [Fact]
    public void TopDown_SelectionIsCaseInsensitive()
    {
        var entity = new GlueEntity();
        GlueMovementValues.ApplyTopDown(entity, TwoMovements());

        entity.SetTopDownMovement("fast");

        entity.TopDown.MovementValues!.MaxSpeed.ShouldBe(300f);
    }
}
