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
        public AARect Body { get; private set; } = null!;
        public float CellSize { get; set; } = 16f;
        public override void CustomInitialize()
        {
            Body = new AARect { Width = CellSize, Height = CellSize };
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
    public void IsSolidGrid_DefaultFalse_LeavesSolidSidesAll()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen);

        var a = factory.Create();
        a.X = 0; a.Y = 0;
        var b = factory.Create();
        b.X = 16; b.Y = 0;

        a.Body.SolidSides.ShouldBe(SolidSides.All);
        b.Body.SolidSides.ShouldBe(SolidSides.All);
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
        left.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Right);
        right.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Left);
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

        left.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Right);
        mid.Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Left & ~SolidSides.Right);
        right.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Left);
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
        bricks[0, 1].Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Down & ~SolidSides.Up);
        // (2,1) right-middle: symmetric.
        bricks[2, 1].Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Down & ~SolidSides.Up);
        // (1,0) bottom-middle: lost Up (center gone), still has neighbors left (0,0) and right (2,0).
        bricks[1, 0].Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Left & ~SolidSides.Right);
        // (1,2) top-middle: symmetric.
        bricks[1, 2].Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Left & ~SolidSides.Right);
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

        a.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Right);
        c.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Left);
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
        x.Body.SolidSides.ShouldBe(SolidSides.All);
        y.Body.SolidSides.ShouldBe(SolidSides.All);
        v.Body.SolidSides.ShouldBe(SolidSides.All);
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
        a.Body.SolidSides.ShouldBe(SolidSides.All);
        c.Body.SolidSides.ShouldBe(SolidSides.All);
    }

    [Fact]
    public void IsSolidGrid_PooledFactory_RecycledBrickReindexesAtNewCell()
    {
        // Pooling preserves the body across recycles; the grid index must follow the new position.
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<GridBrick>(screen) { IsSolidGrid = true }.EnablePooling();

        GridBrick a, b;
        using (factory.BeginGridBatch())
        {
            a = factory.Create(); a.X = 0;  a.Y = 0;
            b = factory.Create(); b.X = 16; b.Y = 0;
        }
        b.Destroy(); // returns to free list, grid cleanup runs

        GridBrick c;
        using (factory.BeginGridBatch())
        {
            c = factory.Create(); c.X = 32; c.Y = 0; // recycle of b at a non-adjacent cell
        }

        c.ShouldBeSameAs(b);
        // a at col 0, c at col 2 — no adjacency, all faces restored.
        a.Body.SolidSides.ShouldBe(SolidSides.All);
        c.Body.SolidSides.ShouldBe(SolidSides.All);
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

        a.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Right);
        b.Body.SolidSides.ShouldBe(
            SolidSides.All & ~SolidSides.Left & ~SolidSides.Right);
        c.Body.SolidSides.ShouldBe(SolidSides.All & ~SolidSides.Left);
    }

    private class InitTrackingEntity : Entity
    {
        public static int InitCount;
        public override void CustomInitialize() => InitCount++;
    }

    private class PoolableBullet : Entity
    {
        public AARect Body { get; private set; } = null!;
        public int InitCount;
        public int ResetCount;
        public int CustomDestroyCount;
        public int Lifetime;
        public override void CustomInitialize()
        {
            InitCount++;
            Body = new AARect { Width = 4, Height = 2 };
            Add(Body);
        }
        protected override void Reset()
        {
            ResetCount++;
            Lifetime = 0;
        }
        public override void CustomDestroy() => CustomDestroyCount++;
    }

    [Fact]
    public void Create_AfterDestroyOnPooledFactory_ResetsEngineState()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling();

        var first = factory.Create();
        first.X = 100; first.Y = 50; first.VelocityX = 200;
        first.Destroy();

        var second = factory.Create();

        second.ShouldBeSameAs(first);
        second.X.ShouldBe(0);
        second.Y.ShouldBe(0);
        second.VelocityX.ShouldBe(0);
    }

    [Fact]
    public void Create_AfterDestroyOnPooledFactory_ReturnsSameInstance()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling();

        var first = factory.Create();
        first.Destroy();
        var second = factory.Create();

        second.ShouldBeSameAs(first);
        factory.Instances.ShouldHaveSingleItem();
    }

    [Fact]
    public void CustomDestroy_OnPooledFactoryDestroy_DoesNotRun()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling();

        var entity = factory.Create();
        entity.Destroy();

        entity.CustomDestroyCount.ShouldBe(0);
    }

    [Fact]
    public void CustomInitialize_OnPooledFactory_RunsOncePerInstance()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling();

        var entity = factory.Create();
        entity.Destroy();
        factory.Create();
        entity.Destroy();
        factory.Create();

        entity.InitCount.ShouldBe(1);
    }

    [Fact]
    public void EnablePooling_AfterFirstCreate_Throws()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen);
        factory.Create();

        Should.Throw<System.InvalidOperationException>(() => factory.EnablePooling());
    }

    [Fact]
    public void Prewarm_OnPooledFactory_DoesNotAddToInstances()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling().Prewarm(3);

        factory.Instances.ShouldBeEmpty();

        // Three Creates after prewarm should reuse those instances rather than allocating new ones.
        var a = factory.Create();
        var b = factory.Create();
        var c = factory.Create();
        a.InitCount.ShouldBe(1);
        b.InitCount.ShouldBe(1);
        c.InitCount.ShouldBe(1);
    }

    [Fact]
    public void Reset_OnPooledFactoryRecycle_RunsOnceNotOnFirstCreate()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PoolableBullet>(screen).EnablePooling();

        var entity = factory.Create();
        entity.ResetCount.ShouldBe(0); // not called on first create

        entity.Lifetime = 99;
        entity.Destroy();
        factory.Create();

        entity.ResetCount.ShouldBe(1);
        entity.Lifetime.ShouldBe(0);
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
