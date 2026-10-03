using System.Linq;
using System.Text;
using FlatRedBall.AnimationChain;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

// .achj (JSON) dialect added alongside .achx (XML), mirroring FlatRedBall2.AnimationEditorCommon's
// support (#872 follow-up) -- this package has no dependency on that assembly, so the JSON
// read/write is a separate implementation over this package's own Content types.
public class AnimationChainListSaveJsonTests
{
    private static Func<string, Stream> JsonStream(string json) =>
        _ => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void FromJsonFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        string requestedPath = "Content/Animations/Player.achj";
        string? observedPath = null;
        string json = "{\"animationChains\":[{\"name\":\"Walk\",\"frames\":[" +
            "{\"textureName\":\"walk.png\",\"frameLength\":0.1}]}]}";

        var save = AnimationChainListSave.FromJsonFile(requestedPath, path =>
        {
            observedPath = path;
            return JsonStream(json)(path);
        });

        // AnimationEditorCommon's FromJsonFile stores FileName verbatim (not resolved to an
        // absolute path).
        Assert.Equal(requestedPath, save.FileName);
        Assert.Equal("Walk", save.AnimationChains[0].Name);
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
            FlipDiagonal = true,
            RelativeX = 3.5f,
            Red = 200,
            ColorOperation = ColorOperation.Add,
        });
        save.AnimationChains.Add(chain);

        var roundTripped = AnimationChainListSave.FromJsonString(save.ToJsonString());

        var frame = roundTripped.AnimationChains[0].Frames[0];
        Assert.Equal("walk.png", frame.TextureName);
        Assert.Equal(0.25f, frame.FrameLength, precision: 5);
        Assert.True(frame.FlipDiagonal);
        Assert.Equal(3.5f, frame.RelativeX, precision: 5);
        Assert.Equal(200, frame.Red);
        Assert.Null(frame.Green);
        Assert.Equal(ColorOperation.Add, frame.ColorOperation);
    }

    [Fact]
    public void ToJsonString_FrameWithRectangleShape_RoundTripsShapeData()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Attack" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Add(new AARectSave { Name = "Sword", X = 5, Y = 1, ScaleX = 15, ScaleY = 5 });
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var roundTripped = AnimationChainListSave.FromJsonString(save.ToJsonString());

        var rect = roundTripped.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Single();
        Assert.Equal("Sword", rect.Name);
        Assert.Equal(5f, rect.X, precision: 5);
        Assert.Equal(15f, rect.ScaleX, precision: 5);
    }

    // ─── FromStream (auto-detect XML vs JSON from content) ────────────────

    [Fact]
    public void FromStream_JsonContent_ParsesAsJson()
    {
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(save.ToJsonString()));

        var loaded = AnimationChainListSave.FromStream(stream);

        Assert.Equal("Walk", loaded.AnimationChains[0].Name);
    }

    [Fact]
    public void FromStream_XmlContent_ParsesAsXml()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <AnimationChain><Name>Run</Name></AnimationChain>
            </AnimationChainArraySave>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var loaded = AnimationChainListSave.FromStream(stream);

        Assert.Equal("Run", loaded.AnimationChains[0].Name);
    }

    [Fact]
    public void FromStream_JsonContentWithLeadingWhitespace_StillDetectsJson()
    {
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("   \n" + save.ToJsonString()));

        var loaded = AnimationChainListSave.FromStream(stream);

        Assert.Equal("Idle", loaded.AnimationChains[0].Name);
    }

    [Fact]
    public void SaveJson_FromJsonFile_RoundTripsThroughDisk()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "idle.png", FrameLength = 0.5f });
        save.AnimationChains.Add(chain);

        var tempPath = Path.GetTempFileName() + ".achj";
        try
        {
            save.SaveJson(tempPath);
            var loaded = AnimationChainListSave.FromJsonFile(tempPath);

            Assert.Equal("Idle", loaded.AnimationChains[0].Name);
            Assert.Equal("idle.png", loaded.AnimationChains[0].Frames[0].TextureName);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
