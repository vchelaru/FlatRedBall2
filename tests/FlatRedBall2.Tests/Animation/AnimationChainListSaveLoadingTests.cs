using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Verifies that AnimationChainListSave.FromFile reads .achx bytes through an injectable seam
// — proof that the engine no longer hard-codes a File.IO path and can route reads through
// TitleContainer (required for KNI Blazor / WASM, where there is no filesystem).
[Collection("AnimationChainListSaveStaticState")]
public class AnimationChainListSaveLoadingTests
{
    [Fact]
    public void FromFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        var originalProvider = AnimationChainListSave.StreamProvider;
        try
        {
            string requestedPath = "Content/Animations/ShmupSpace.achx";
            string? observedPath = null;

            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<AnimationChainArraySave>" +
                "  <AnimationChain><Name>Walk</Name>" +
                "    <Frame><TextureName>walk.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
                "  </AnimationChain>" +
                "</AnimationChainArraySave>";

            AnimationChainListSave.StreamProvider = path =>
            {
                observedPath = path;
                return new MemoryStream(Encoding.UTF8.GetBytes(xml));
            };

            var save = AnimationChainListSave.FromFile(requestedPath);

            observedPath.ShouldBe(requestedPath);
            save.AnimationChains.Count.ShouldBe(1);
            save.AnimationChains[0].Name.ShouldBe("Walk");
        }
        finally
        {
            AnimationChainListSave.StreamProvider = originalProvider;
        }
    }
}
