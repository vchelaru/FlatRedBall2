using System;
using System.IO;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Per-frame Red/Green/Blue (0-255, nullable) are stored on the frame and surfaced to game code,
// but are NOT applied by the engine — game code reads Sprite.CurrentFrame and decides how to use them.
public class AnimationFrameColorTests
{
    [Fact]
    public void CurrentFrame_ReflectsPlaybackState()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1), Red = 255 });
        var list = new AnimationChainList();
        list.Add(chain);

        var sprite = new Sprite { AnimationChains = list };

        sprite.CurrentFrame.ShouldBeNull(); // nothing playing yet

        sprite.PlayAnimation("Flash");

        sprite.CurrentFrame.ShouldNotBeNull();
        sprite.CurrentFrame!.Red.ShouldBe(255);
    }

    [Fact]
    public void Save_ThenFromFile_RoundTripsColorOperationAndOmitsNull()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Flash" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", ColorOperation = ColorOperation.Add });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png" }); // no operation -> element omitted
        save.AnimationChains.Add(chain);

        var path = Path.Combine(Path.GetTempPath(), $"frbcolorop_{Guid.NewGuid():N}.achx");
        try
        {
            save.Save(path);
            // Only the first frame authored an operation; the null frame must omit the element.
            (File.ReadAllText(path).Split("<ColorOperation>").Length - 1).ShouldBe(1);

            var frames = AnimationChainListSave.FromFile(path).AnimationChains[0].Frames;
            frames[0].ColorOperation.ShouldBe(ColorOperation.Add);
            frames[1].ColorOperation.ShouldBeNull();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Save_ThenFromFile_RoundTripsSetChannelsAndOmitsNullChannel()
    {
        // Red and Blue are authored; Green is left null to prove null channels are not written.
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Flash" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "car.png", FrameLength = 0.1f, Red = 255, Blue = 128 });
        save.AnimationChains.Add(chain);

        var path = Path.Combine(Path.GetTempPath(), $"frbcolor_{Guid.NewGuid():N}.achx");
        try
        {
            save.Save(path);

            File.ReadAllText(path).ShouldNotContain("<Green>"); // null channel omitted

            var frame = AnimationChainListSave.FromFile(path).AnimationChains[0].Frames[0];
            frame.Red.ShouldBe(255);
            frame.Blue.ShouldBe(128);
            frame.Green.ShouldBeNull();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ToAnimationChainList_CopiesColorChannelsToRuntimeFrame()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Flash" };
        // Empty TextureName so conversion skips texture loading and the null ContentLoader is never used.
        chain.Frames.Add(new AnimationFrameSave { Red = 255, Green = 200, Blue = 128 });
        save.AnimationChains.Add(chain);

        var frame = save.ToAnimationChainList(null!)[0][0];

        frame.Red.ShouldBe(255);
        frame.Green.ShouldBe(200);
        frame.Blue.ShouldBe(128);
    }

    [Fact]
    public void ToAnimationChainList_CopiesColorOperationToRuntimeFrame()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Flash" };
        chain.Frames.Add(new AnimationFrameSave { ColorOperation = ColorOperation.Multiply });
        save.AnimationChains.Add(chain);

        var frame = save.ToAnimationChainList(null!)[0][0];

        frame.ColorOperation.ShouldBe(ColorOperation.Multiply);
    }
}
