using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Verifies that AdobeAnimateAtlasSave.FromFile reads atlas bytes through an injectable seam
// — proof that the engine no longer hard-codes a File.IO path and can route reads through
// TitleContainer (required for KNI Blazor / WASM, where there is no filesystem).
public class AdobeAnimateAtlasSaveLoadingTests
{
    [Fact]
    public void FromFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        var originalProvider = AdobeAnimateAtlasSave.StreamProvider;
        try
        {
            string requestedPath = "Content/Animations/Eyeball.xml";
            string? observedPath = null;

            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<TextureAtlas imagePath=\"Eyeball.png\">" +
                "  <SubTexture name=\"Idle0000\" x=\"0\" y=\"0\" width=\"32\" height=\"32\"/>" +
                "</TextureAtlas>";

            AdobeAnimateAtlasSave.StreamProvider = path =>
            {
                observedPath = path;
                return new MemoryStream(Encoding.UTF8.GetBytes(xml));
            };

            var save = AdobeAnimateAtlasSave.FromFile(requestedPath);

            observedPath.ShouldBe(requestedPath);
            save.ImagePath.ShouldBe("Eyeball.png");
            save.SubTextures.Count.ShouldBe(1);
        }
        finally
        {
            AdobeAnimateAtlasSave.StreamProvider = originalProvider;
        }
    }
}
