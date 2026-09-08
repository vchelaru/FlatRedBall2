using System.IO;
using System.Linq;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Verifies the .achj (JSON) dialect added alongside .achx (XML): AnimationChainListSave can
// round-trip chains, frames, and shapes through ToJsonString/FromJsonString and SaveJson/FromJsonFile.
public class AnimationChainListSaveJsonTests
{
    [Fact]
    public void FromJsonFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        string requestedPath = "Content/Animations/ShmupSpace.achj";
        string? observedPath = null;
        string json = "{\"animationChains\":[{\"name\":\"Walk\",\"frames\":[" +
            "{\"textureName\":\"walk.png\",\"frameLength\":0.1}]}]}";

        var save = AnimationChainListSave.FromJsonFile(requestedPath, path =>
        {
            observedPath = path;
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        });

        observedPath.ShouldBe(requestedPath);
        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void ToJsonString_FromJsonString_ChainIsLockedTrue_RoundTrips()
    {
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk", IsLocked = true });

        var json = save.ToJsonString();
        var roundTripped = AnimationChainListSave.FromJsonString(json);

        json.ShouldContain("\"locked\": true");
        roundTripped.AnimationChains.Single().IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void ToJsonString_ChainNotLocked_OmitsLockedKey()
    {
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });

        var json = save.ToJsonString();

        json.ShouldNotContain("locked");
    }

    [Fact]
    public void ToJsonString_FromJsonString_RoundTripsFrameFields()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "walk.png",
            FrameLength = 0.25f,
            LeftCoordinate = 0.1f,
            RightCoordinate = 0.9f,
            TopCoordinate = 0.2f,
            BottomCoordinate = 0.8f,
            FlipHorizontal = true,
            RelativeX = 3.5f,
            Red = 200,
            Alpha = 128,
            ColorOperation = ColorOperation.Add,
        });
        save.AnimationChains.Add(chain);

        var roundTripped = AnimationChainListSave.FromJsonString(save.ToJsonString());

        var frame = roundTripped.AnimationChains.Single().Frames.Single();
        frame.TextureName.ShouldBe("walk.png");
        frame.FrameLength.ShouldBe(0.25f);
        frame.LeftCoordinate.ShouldBe(0.1f);
        frame.RightCoordinate.ShouldBe(0.9f);
        frame.FlipHorizontal.ShouldBeTrue();
        frame.FlipVertical.ShouldBeFalse();
        frame.RelativeX.ShouldBe(3.5f);
        frame.Red.ShouldBe(200);
        frame.Green.ShouldBeNull();
        frame.Alpha.ShouldBe(128);
        frame.ColorOperation.ShouldBe(ColorOperation.Add);
    }

    [Fact]
    public void ToJsonString_FrameWithRectangleShape_RoundTripsShapeData()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Attack" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "Sword", X = 5, Y = 1, ScaleX = 15, ScaleY = 5 });
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var roundTripped = AnimationChainListSave.FromJsonString(save.ToJsonString());

        var rect = roundTripped.AnimationChains.Single().Frames.Single().ShapesSave!.AARectSaves.Single();
        rect.Name.ShouldBe("Sword");
        rect.X.ShouldBe(5f);
        rect.ScaleX.ShouldBe(15f);
        rect.ScaleY.ShouldBe(5f);
    }

    [Fact]
    public void SaveJson_FromJsonFile_RoundTripsThroughDisk()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "idle.png", FrameLength = 0.5f });
        save.AnimationChains.Add(chain);

        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achj");
        try
        {
            save.SaveJson(tempPath);
            var loaded = AnimationChainListSave.FromJsonFile(tempPath);

            loaded.AnimationChains.Single().Name.ShouldBe("Idle");
            loaded.AnimationChains.Single().Frames.Single().TextureName.ShouldBe("idle.png");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
