using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Diagnostics;

public class RenderDiagnosticsTests
{
    private class TestScreen : Screen { }

    // Minimal IRenderBatch that never touches the SpriteBatch it's given — lets Screen.Draw
    // run headlessly (no GraphicsDevice) in these tests.
    private class StubBatch : IRenderBatch
    {
        public int InternalDrawCallCountToReport;
        public bool FlipsY => false;
        public void Begin(SpriteBatch spriteBatch, Camera camera) { }
        public void End(SpriteBatch spriteBatch) { }
        public int InternalDrawCallCount => InternalDrawCallCountToReport;
    }

    // IRenderBatch implementer that does not override InternalDrawCallCount at all —
    // proves the interface default applies without every existing batch changing.
    private class DefaultOnlyBatch : IRenderBatch
    {
        public bool FlipsY => false;
        public void Begin(SpriteBatch spriteBatch, Camera camera) { }
        public void End(SpriteBatch spriteBatch) { }
    }

    private class StubRenderable : IRenderable
    {
        public float Z { get; set; }
        public Layer? Layer { get; set; }
        public required IRenderBatch Batch { get; set; }
        public string? Name { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool WasDrawn { get; private set; }
        public void Draw(SpriteBatch spriteBatch, Camera camera) => WasDrawn = true;
    }

    // IRenderable implementer that does not override IsVisible at all —
    // proves the interface default applies without every existing renderable changing.
    private class DefaultVisibilityRenderable : IRenderable
    {
        public float Z { get; set; }
        public Layer? Layer { get; set; }
        public required IRenderBatch Batch { get; set; }
        public string? Name { get; set; }
        public void Draw(SpriteBatch spriteBatch, Camera camera) { }
    }

    [Fact]
    public void IRenderBatch_NoOverride_InternalDrawCallCountDefaultsToZero()
    {
        IRenderBatch batch = new DefaultOnlyBatch();
        batch.InternalDrawCallCount.ShouldBe(0);
    }

    [Fact]
    public void IRenderable_NoOverride_IsVisibleDefaultsToTrue()
    {
        IRenderable renderable = new DefaultVisibilityRenderable { Batch = new DefaultOnlyBatch() };
        renderable.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Screen_Draw_InvisibleRenderableBetweenSameBatchNeighbors_ForcesNoBatchBreakAndIsNotDrawn()
    {
        // Mirrors CardEntity: a Gum visual and an invisible collision AARect (different batches)
        // sandwiched at the same Z. Before IRenderable.IsVisible existed, the invisible shape
        // still forced a real Begin/End round-trip because the batch-transition check ran before
        // any visibility check.
        var screen = new TestScreen();
        var layer = new Layer("Test");
        screen.Layers.Add(layer);
        var diagnostics = new RenderDiagnostics { IsEnabled = true };
        var camera = new Camera();

        var gumLikeBatch = new StubBatch();
        var shapesLikeBatch = new StubBatch();

        var before = new StubRenderable { Z = 0f, Layer = layer, Name = "before", Batch = gumLikeBatch };
        var invisibleShape = new StubRenderable { Z = 1f, Layer = layer, Name = "hitbox", Batch = shapesLikeBatch, IsVisible = false };
        var after = new StubRenderable { Z = 2f, Layer = layer, Name = "after", Batch = gumLikeBatch };

        screen.Add(before);
        screen.Add(invisibleShape);
        screen.Add(after);

        diagnostics.BeginFrame();
        screen.Draw(null!, diagnostics, camera);

        diagnostics.BatchBreakCount.ShouldBe(0);
        before.WasDrawn.ShouldBeTrue();
        after.WasDrawn.ShouldBeTrue();
        invisibleShape.WasDrawn.ShouldBeFalse();
    }

    [Fact]
    public void RecordInternalDrawCalls_CalledTwiceInOneFrame_KeepsLargestReportInsteadOfSumming()
    {
        // GumRenderBatch.InternalDrawCallCount reads Gum's own cumulative-for-the-host-frame
        // counter (reset once per frame by Gum, not once per Begin/End cycle), and Gum's Begin/End
        // runs multiple times per frame (once per camera, plus the overlay pass). Each report
        // already includes every earlier cycle's draws, so recording it again must overwrite, not
        // add — summing would double-count the earlier cycles every time.
        var diagnostics = new RenderDiagnostics { IsEnabled = true };

        diagnostics.RecordInternalDrawCalls(6); // cycle 1 end: cumulative so far
        diagnostics.RecordInternalDrawCalls(7); // cycle 2 end: cumulative so far (includes cycle 1)

        diagnostics.InternalDrawCallCount.ShouldBe(7);
    }

    [Fact]
    public void BeginFrame_ResetsInternalDrawCallCount()
    {
        var diagnostics = new RenderDiagnostics { IsEnabled = true };
        diagnostics.RecordInternalDrawCalls(9);

        diagnostics.BeginFrame();

        diagnostics.InternalDrawCallCount.ShouldBe(0);
    }

    [Fact]
    public void Screen_Draw_TwoBatchesInTheFrame_InternalDrawCallCountIsTheLargestReported()
    {
        // In practice only GumRenderBatch overrides InternalDrawCallCount, and it reports a
        // cumulative-for-the-host-frame value (see RecordInternalDrawCalls_CalledTwiceInOneFrame_
        // KeepsLargestReportInsteadOfSumming), so the last Begin/End cycle's report already reflects
        // every draw call so far this frame — Screen must not sum successive cycles' reports.
        var screen = new TestScreen();
        var layer = new Layer("Test");
        screen.Layers.Add(layer);
        var diagnostics = new RenderDiagnostics { IsEnabled = true };
        var camera = new Camera();

        var gumLikeBatch = new StubBatch { InternalDrawCallCountToReport = 40 };
        var shapesLikeBatch = new StubBatch { InternalDrawCallCountToReport = 46 };

        // Two different batches back-to-back force two separate Begin/End pairs.
        screen.Add(new StubRenderable { Z = 0f, Layer = layer, Name = "gum", Batch = gumLikeBatch });
        screen.Add(new StubRenderable { Z = 1f, Layer = layer, Name = "shapes", Batch = shapesLikeBatch });

        diagnostics.BeginFrame();
        screen.Draw(null!, diagnostics, camera);

        diagnostics.InternalDrawCallCount.ShouldBe(46);
    }

    [Fact]
    public void Screen_Draw_NonReportingBatchEndsTheFrame_DoesNotWipeAnEarlierGumReport()
    {
        // Only a batch wrapping a foreign renderer overrides InternalDrawCallCount; every other
        // batch returns the interface default of 0. Screen.EndBatch records whatever each batch it
        // ends reports, so a plain world-space batch closing out the frame must not overwrite the
        // Gum count with its own 0. Each report is a running total for the whole frame, so the
        // frame's answer is the largest report, not the last one to arrive.
        var screen = new TestScreen();
        var layer = new Layer("Test");
        screen.Layers.Add(layer);
        var diagnostics = new RenderDiagnostics { IsEnabled = true };
        var camera = new Camera();

        var gumLikeBatch = new StubBatch { InternalDrawCallCountToReport = 46 };
        var worldSpaceLikeBatch = new DefaultOnlyBatch();

        screen.Add(new StubRenderable { Z = 0f, Layer = layer, Name = "gum", Batch = gumLikeBatch });
        screen.Add(new StubRenderable { Z = 1f, Layer = layer, Name = "sprite", Batch = worldSpaceLikeBatch });

        diagnostics.BeginFrame();
        screen.Draw(null!, diagnostics, camera);

        diagnostics.InternalDrawCallCount.ShouldBe(46);
    }
}
