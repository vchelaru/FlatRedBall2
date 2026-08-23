using System;
using System.IO;
using FlatRedBall2;
using FlatRedBall2.Glue;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Proves the full wiring: an entity whose Glue project authors a .PlatformerAnimations.json sidecar
// discovers it during BuildObjects, and CustomActivity resolves + plays a chain from it every frame —
// with no game code involved. Condition/priority/suffix logic itself is covered exhaustively by
// GlueAnimationEvaluatorTests against PlatformerBehavior directly; this only proves the pipeline.
public class GlueAnimationLayerEndToEndTests
{
    [Fact]
    public void CustomActivity_DoorsDemoPlayerWithAnimationLayersSidecar_PlaysTheWinningChain()
    {
        var project = GlueProject.Load(
            Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

        var entity = new GlueEntity
        {
            Save = project.FindEntity(@"Entities\Player"),
            Content = new GlueContentSource(
                new ContentLoader(), Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content")),
        };

        entity.BuildObjects();

        var frame = new FrameTime(
            TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);
        entity.CustomActivity(frame);

        var sprite = entity.Objects["SpriteInstance"].ShouldBeOfType<Sprite>();
        // Player.PlatformerAnimations.json's only always-true layer is CharacterIdle with
        // HasLeftAndRight — DirectionFacing defaults to Right, so "CharacterIdleRight" wins over the
        // sidecar's CharacterRun layer (velocity is 0, below its MinXVelocityAbsolute of 140).
        sprite.CurrentAnimation?.Name.ShouldBe("CharacterIdleRight");
    }
}
