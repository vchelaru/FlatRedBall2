using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class FactoryTests
{
    private class TestEntity : Entity { }
    private class TestScreen : Screen { }

    // Grid entity: 16x16 body, attached during CustomInitialize; X/Y set by caller AFTER Create.
    // For IsSolidGrid the caller must wrap Create calls in BeginGridBatch because indexing reads
    // the body's absolute position, which depends on entity X/Y being set.
    private class GridBrick : Entity
    {
        public AxisAlignedRectangle Body { get; private set; } = null!;
        public float CellSize { get; set; } = 16f;
        public override void CustomInitialize()
        {
            Body = new AxisAlignedRectangle { Width = CellSize, Height = CellSize };
            Add(Body);
        }
    }

    [Fact]
    public void Create_AddsToInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        factory.Create();

        factory.Instances.ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_CallsCustomInitialize()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<InitTrackingEntity>(screen);
        InitTrackingEntity.InitCount = 0;

        factory.Create();

        InitTrackingEntity.InitCount.ShouldBe(1);
    }

    [Fact]
    public void Destroy_CalledDirectlyOnEntity_RemovesFromFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();
        entity.Destroy(); // no factory reference needed

        factory.Instances.ShouldBeEmpty();
    }

    [Fact]
    public void Destroy_DuringEnumeration_DoesNotThrow()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);

        var e1 = factory.Create();
        factory.Create();

        // Simulates destroying inside CollisionOccurred while the collision loop iterates the factory
        Should.NotThrow(() =>
        {
            foreach (var _ in factory)
                factory.Destroy(e1);
        });

        factory.Instances.Count.ShouldBe(1);
    }

    [Fact]
    public void Destroy_RemovesFromInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        var entity = factory.Create();
        factory.Destroy(entity);

        factory.Instances.ShouldBeEmpty();
    }

    [Fact]
    public void DestroyAll_ClearsAllInstances()
    {
        var screen = new TestScreen();
        var engine = new FlatRedBallService();
        screen.Engine = engine;
        var factory = new Factory<TestEntity>(screen);

        factory.Create();
        factory.Create();
        factory.Create();
        factory.DestroyAll();

        factory.Instances.ShouldBeEmpty();
    }

    [Fact]
    public void IsSolidGrid_DefaultFalse_LeavesRepositionDirectionsAll()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen);

        var a = factory.Create();
        a.X = 0; a.Y = 0;
        var b = factory.Create();
        b.X = 16; b.Y = 0;

        a.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
        b.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void IsSolidGrid_HorizontalPair_SharedFacesSuppressed()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        // Bulk-spawn pattern: batch because Create happens before X/Y is set.
        GridBrick left, right;
        using (factory.BeginGridBatch())
        {
            left = factory.Create();  left.X = 0;  left.Y = 0;
            right = factory.Create(); right.X = 16; right.Y = 0;
        }

        // Left brick: right face suppressed (neighbor to the right), others active.
        left.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Right);
        right.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Left);
    }

    [Fact]
    public void IsSolidGrid_HalfCellOffsetPositions_AdjacencyStillWorks()
    {
        // Regression: tile-layer spawns place bodies at tile centers, which are half-cell offset
        // from origin. With MathF.Round (banker's rounding) two adjacent bricks at X=24 and X=40
        // both round to col 2, collapsing the grid. Floor yields col 1 and col 2 — correct.
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        GridBrick left, mid, right;
        using (factory.BeginGridBatch())
        {
            left = factory.Create();  left.X  = 8;  left.Y  = 8;
            mid = factory.Create();   mid.X   = 24; mid.Y   = 8;
            right = factory.Create(); right.X = 40; right.Y = 8;
        }

        left.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Right);
        mid.Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Left & ~RepositionDirections.Right);
        right.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Left);
    }

    [Fact]
    public void IsSolidGrid_MismatchedCellSize_Throws()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        using var batch = factory.BeginGridBatch();
        var a = factory.Create(); a.X = 0; a.Y = 0;         // 16x16 body
        var b = factory.Create(); b.CellSize = 8f;           // changes CellSize; rebuild body
        b.Body.Width = 8f; b.Body.Height = 8f;
        b.X = 8; b.Y = 0;

        Should.Throw<System.InvalidOperationException>(() => batch.Dispose());
    }

    [Fact]
    public void IsSolidGrid_Destroy3x3Center_RestoresInteriorFacesOfOrthogonalNeighbors()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        // 3x3 grid. bricks[col, row]. With Y+ up, row=0 is the bottom row.
        var bricks = new GridBrick[3, 3];
        using (factory.BeginGridBatch())
        {
            for (int col = 0; col < 3; col++)
                for (int row = 0; row < 3; row++)
                {
                    var brick = factory.Create();
                    brick.X = col * 16; brick.Y = row * 16;
                    bricks[col, row] = brick;
                }
        }

        bricks[1, 1].Destroy();

        // (0,1) left-middle: lost Right (center gone), still has neighbors below (0,0) and above (0,2).
        bricks[0, 1].Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Down & ~RepositionDirections.Up);
        // (2,1) right-middle: symmetric.
        bricks[2, 1].Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Down & ~RepositionDirections.Up);
        // (1,0) bottom-middle: lost Up (center gone), still has neighbors left (0,0) and right (2,0).
        bricks[1, 0].Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Left & ~RepositionDirections.Right);
        // (1,2) top-middle: symmetric.
        bricks[1, 2].Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Left & ~RepositionDirections.Right);
    }

    [Fact]
    public void IsSolidGrid_DestroyThenCreateAdjacent_NewNeighborSuppressesFace()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        GridBrick a, b;
        using (factory.BeginGridBatch())
        {
            a = factory.Create(); a.X = 0;  a.Y = 0;
            b = factory.Create(); b.X = 16; b.Y = 0;
        }

        b.Destroy();

        // Spawn a new brick at col=1 (where B was). A's Right face should be suppressed again because
        // C is now the new neighbor — verifies post-destroy creates still wire into neighbor updates.
        GridBrick c;
        using (factory.BeginGridBatch())
        {
            c = factory.Create(); c.X = 16; c.Y = 0;
        }

        a.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Right);
        c.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Left);
    }

    [Fact]
    public void IsSolidGrid_DestroyTwoAdjacentInRow_SharedFaceBetweenSurvivorsStaysSuppressed()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        GridBrick x, w, y, z, v;
        using (factory.BeginGridBatch())
        {
            x = factory.Create(); x.X = 0;  x.Y = 0;
            w = factory.Create(); w.X = 16; w.Y = 0;
            y = factory.Create(); y.X = 32; y.Y = 0;
            z = factory.Create(); z.X = 48; z.Y = 0;
            v = factory.Create(); v.X = 64; v.Y = 0;
        }

        w.Destroy();
        z.Destroy();

        // Survivors X, Y, V at cols 0, 2, 4 — none adjacent. All faces restored.
        x.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
        y.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
        v.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void IsSolidGrid_RemoveMiddleOf3_RestoresInnerFacesOnRemaining()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        GridBrick a, b, c;
        using (factory.BeginGridBatch())
        {
            a = factory.Create(); a.X = 0;  a.Y = 0;
            b = factory.Create(); b.X = 16; b.Y = 0;
            c = factory.Create(); c.X = 32; c.Y = 0;
        }

        b.Destroy();

        // A was missing its Right face (neighbor b) — should now be All again.
        a.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
        c.Body.RepositionDirections.ShouldBe(RepositionDirections.All);
    }

    [Fact]
    public void IsSolidGrid_Row3_MiddleHasBothSideFacesSuppressed()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true };

        GridBrick a, b, c;
        using (factory.BeginGridBatch())
        {
            a = factory.Create(); a.X = 0;  a.Y = 0;
            b = factory.Create(); b.X = 16; b.Y = 0;
            c = factory.Create(); c.X = 32; c.Y = 0;
        }

        a.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Right);
        b.Body.RepositionDirections.ShouldBe(
            RepositionDirections.All & ~RepositionDirections.Left & ~RepositionDirections.Right);
        c.Body.RepositionDirections.ShouldBe(RepositionDirections.All & ~RepositionDirections.Left);
    }

    private class InitTrackingEntity : Entity
    {
        public static int InitCount;
        public override void CustomInitialize() => InitCount++;
    }

    // Reads InitOnlyValue inside CustomInitialize and stamps it into ObservedValue.
    // The configure callback on Create must run BEFORE CustomInitialize for ObservedValue
    // to reflect what the caller passed in.
    private class ConfigurableEntity : Entity
    {
        public int InitOnlyValue { get; set; }
        public int ObservedValue { get; private set; } = -1;
        public override void CustomInitialize() => ObservedValue = InitOnlyValue;
    }

    [Fact]
    public void Create_WithConfigure_RunsConfigureBeforeCustomInitialize()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<ConfigurableEntity>(screen);

        var entity = factory.Create(e => e.InitOnlyValue = 42);

        entity.ObservedValue.ShouldBe(42);
    }

    [Fact]
    public void Create_WithConfigure_RegistersInstance()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<ConfigurableEntity>(screen);

        factory.Create(_ => { });

        factory.Instances.ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_WithNullConfigure_Throws()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<ConfigurableEntity>(screen);

        Should.Throw<System.ArgumentNullException>(() => factory.Create(null!));
    }
}
