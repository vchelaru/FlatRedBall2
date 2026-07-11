using System;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Issue #593. Same testability constraint as SpriteRotationTests (#378): no headless
// GraphicsDevice, so no pixel readback — but MonoGame's SpriteEffects has no diagonal-transpose
// value either, so there is no flag to plumb through SpriteBatch.Draw in the first place. Instead
// FlipDiagonal is implemented as a +/-90 degree rotation offset combined with a single vertical-
// mirror choice (Sprite.ComputeDrawTransform) — a reflection is always expressible as a rotation
// composed with one axis mirror. That composition is verified here against an independent ground
// truth: the same (a,b,c,d) transpose matrix used by the Animation Editor's already-tested
// AnimationEditor.Core.Rendering.FlipScaleCalculator.ComputeMatrix, which drives the SkiaSharp
// preview this issue is required to match. Both sides are pure CPU math — no GraphicsDevice.
public class SpriteDiagonalFlipTests
{
    // Ground truth: FlipScaleCalculator.ComputeMatrix(h, v, flipDiagonal: true) is (0, b, c, 0)
    // with b = h?-1:1, c = v?-1:1 (diagonal-only is the plain swap (x,y)->(y,x) — it fixes the
    // top-left/bottom-right corners and swaps top-right/bottom-left, matching Tiled's actual
    // diagonal-flip semantics), mapping local offset (u,v) -> (b*v, c*u). Applied here to a
    // scaled offset and then the sprite's own base rotation, matching how Sprite composes flip
    // with an already-set Rotation.
    static (float x, float y) ExpectedOffset(bool flipHorizontal, bool effectiveFlipVertical, float baseRotationRadians, float u, float v, float scaleX, float scaleY)
    {
        float b = flipHorizontal ? -1f : 1f;
        float c = effectiveFlipVertical ? -1f : 1f;
        float outX = b * (v * scaleY);
        float outY = c * (u * scaleX);
        float cr = MathF.Cos(baseRotationRadians), sr = MathF.Sin(baseRotationRadians);
        return (outX * cr - outY * sr, outX * sr + outY * cr);
    }

    // The formula SpriteBatch actually computes on the CPU (see SpriteRotationTests), driven by
    // the (rotation, effects) Sprite.ComputeDrawTransform() hands to spriteBatch.Draw.
    static (float x, float y) ActualOffset(Sprite sprite, float u, float v, float scaleX, float scaleY)
    {
        var (rotation, effects) = sprite.ComputeDrawTransform();
        float dx = ((effects & SpriteEffects.FlipHorizontally) != 0 ? -u : u) * scaleX;
        float dy = ((effects & SpriteEffects.FlipVertically) != 0 ? -v : v) * scaleY;
        float cr = MathF.Cos(rotation), sr = MathF.Sin(rotation);
        return (dx * cr - dy * sr, dx * sr + dy * cr);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ComputeDrawTransform_DiagonalFlip_MatchesTransposeMatrix_WorldSpace(bool flipHorizontal, bool flipVertical)
    {
        var sprite = new Sprite
        {
            FlipDiagonal = true,
            FlipHorizontal = flipHorizontal,
            FlipVertical = flipVertical,
            Batch = WorldSpaceBatch.Instance, // FlipsY == true
        };
        bool effectiveFlipVertical = true ^ flipVertical;

        AssertMatchesAcrossSamples(sprite, flipHorizontal, effectiveFlipVertical);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ComputeDrawTransform_DiagonalFlip_MatchesTransposeMatrix_ScreenSpace(bool flipHorizontal, bool flipVertical)
    {
        var sprite = new Sprite
        {
            FlipDiagonal = true,
            FlipHorizontal = flipHorizontal,
            FlipVertical = flipVertical,
            Batch = ScreenSpaceBatch.Instance, // FlipsY == false
        };
        bool effectiveFlipVertical = false ^ flipVertical;

        AssertMatchesAcrossSamples(sprite, flipHorizontal, effectiveFlipVertical);
    }

    static void AssertMatchesAcrossSamples(Sprite sprite, bool flipHorizontal, bool effectiveFlipVertical)
    {
        float[] baseRotations = { 0f, 0.4f, -1.1f, MathF.PI / 2f, 2.9f };
        (float u, float v)[] points = { (0.3f, 0.7f), (1f, 0f), (0f, 1f), (-0.5f, 0.9f) };
        (float sx, float sy)[] scales = { (1f, 1f), (2f, 0.5f), (0.3f, 3f) }; // non-square — footprint swap must hold generally

        foreach (var baseRotation in baseRotations)
        {
            sprite.Rotation = FlatRedBall2.Math.Angle.FromRadians(baseRotation);
            foreach (var (u, v) in points)
            foreach (var (sx, sy) in scales)
            {
                var expected = ExpectedOffset(flipHorizontal, effectiveFlipVertical, sprite.RenderRotationRadians, u, v, sx, sy);
                var actual = ActualOffset(sprite, u, v, sx, sy);

                actual.x.ShouldBe(expected.x, 1e-4);
                actual.y.ShouldBe(expected.y, 1e-4);
            }
        }
    }

    // The user-facing framing: a non-square sprite whose four source corners are known landmarks.
    // After a pure diagonal flip (no H/V), the texture's top-left corner — local offset
    // (-halfW, -halfH) — must stay at the TOP-LEFT of the (now swapped) footprint, matching
    // Tiled's actual diagonal-flip semantics (verified against TileMapCollisionsTests'
    // GenerateFromClass_PolygonTileFlippedDiagonally_PointsReflectedAcrossDiagonal, where the
    // (0,0)/(16,16) corners are provably unchanged by a diagonal-only flip) and
    // AnimationEditorAvalonia's PreviewControl.ComputeFlipMatrix, which the runtime must match.
    // Uses ScreenSpaceBatch (FlipsY == false) so the camera's own Y-flip compensation — which
    // WorldSpaceBatch always folds in, even with no user-facing flip set — doesn't also apply
    // here; that compensation is exercised separately by the exhaustive tests above.
    [Fact]
    public void DiagonalFlipOnly_NonSquareSprite_TopLeftTexelStaysAtTopLeft()
    {
        var sprite = new Sprite { FlipDiagonal = true, Batch = ScreenSpaceBatch.Instance };
        const float halfW = 100f, halfH = 50f; // source 200x100
        var actual = ActualOffset(sprite, u: -halfW, v: -halfH, scaleX: 1f, scaleY: 1f);

        // Swapped footprint: this corner's world offset spans the ORIGINAL half-height on X and
        // the ORIGINAL half-width on Y.
        MathF.Abs(actual.x).ShouldBe(halfH, 1e-4);
        MathF.Abs(actual.y).ShouldBe(halfW, 1e-4);

        // Top-left of the new (swapped) footprint means both offsets are negative.
        actual.x.ShouldBeLessThan(0f);
        actual.y.ShouldBeLessThan(0f);
    }

    // The corner NOT on the diagonal axis must move: top-right (u positive, v negative) ends up
    // at bottom-left of the swapped footprint.
    [Fact]
    public void DiagonalFlipOnly_NonSquareSprite_TopRightTexelMovesToBottomLeft()
    {
        var sprite = new Sprite { FlipDiagonal = true, Batch = ScreenSpaceBatch.Instance };
        const float halfW = 100f, halfH = 50f; // source 200x100
        var actual = ActualOffset(sprite, u: halfW, v: -halfH, scaleX: 1f, scaleY: 1f);

        MathF.Abs(actual.x).ShouldBe(halfH, 1e-4);
        MathF.Abs(actual.y).ShouldBe(halfW, 1e-4);

        // Bottom-left of the new footprint: negative x (left), positive y (down).
        actual.x.ShouldBeLessThan(0f);
        actual.y.ShouldBeGreaterThan(0f);
    }
}
