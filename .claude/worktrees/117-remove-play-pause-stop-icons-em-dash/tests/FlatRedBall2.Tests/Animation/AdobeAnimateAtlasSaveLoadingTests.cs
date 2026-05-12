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
    public void FromFile_MultipleSubTextures_ParsesAllFields()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<TextureAtlas imagePath=\"Sprite.png\">" +
            "  <SubTexture name=\"Walk0000\" x=\"0\" y=\"0\" width=\"32\" height=\"48\"/>" +
            "  <SubTexture name=\"Walk0001\" x=\"32\" y=\"0\" width=\"32\" height=\"48\"/>" +
            "  <SubTexture name=\"Jump0000\" x=\"64\" y=\"0\" width=\"40\" height=\"52\"/>" +
            "</TextureAtlas>";

        var save = AdobeAnimateAtlasSave.FromFile("any.xml",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.SubTextures.Count.ShouldBe(3);
        save.SubTextures[0].Name.ShouldBe("Walk0000");
        save.SubTextures[0].X.ShouldBe(0);
        save.SubTextures[0].Y.ShouldBe(0);
        save.SubTextures[0].Width.ShouldBe(32);
        save.SubTextures[0].Height.ShouldBe(48);
        save.SubTextures[1].X.ShouldBe(32);
        save.SubTextures[2].Name.ShouldBe("Jump0000");
        save.SubTextures[2].Width.ShouldBe(40);
    }

    [Fact]
    public void FromFile_PivotAttributes_ParsedAsFloats()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<TextureAtlas imagePath=\"Hero.png\">" +
            "  <SubTexture name=\"Idle0000\" x=\"0\" y=\"0\" width=\"64\" height=\"64\" pivotX=\"32.5\" pivotY=\"60\"/>" +
            "</TextureAtlas>";

        var save = AdobeAnimateAtlasSave.FromFile("any.xml",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.SubTextures[0].PivotX.ShouldBe(32.5f);
        save.SubTextures[0].PivotY.ShouldBe(60f);
    }

    [Fact]
    public void FromFile_PivotMissing_DefaultsToNaN()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<TextureAtlas imagePath=\"Hero.png\">" +
            "  <SubTexture name=\"Idle0000\" x=\"0\" y=\"0\" width=\"32\" height=\"32\"/>" +
            "</TextureAtlas>";

        var save = AdobeAnimateAtlasSave.FromFile("any.xml",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        float.IsNaN(save.SubTextures[0].PivotX).ShouldBeTrue();
        float.IsNaN(save.SubTextures[0].PivotY).ShouldBeTrue();
    }

    [Fact]
    public void FromFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        string requestedPath = "Content/Animations/Eyeball.xml";
        string? observedPath = null;

        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<TextureAtlas imagePath=\"Eyeball.png\">" +
            "  <SubTexture name=\"Idle0000\" x=\"0\" y=\"0\" width=\"32\" height=\"32\"/>" +
            "</TextureAtlas>";

        var save = AdobeAnimateAtlasSave.FromFile(requestedPath, path =>
        {
            observedPath = path;
            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        });

        observedPath.ShouldBe(requestedPath);
        save.ImagePath.ShouldBe("Eyeball.png");
        save.SubTextures.Count.ShouldBe(1);
    }
}
