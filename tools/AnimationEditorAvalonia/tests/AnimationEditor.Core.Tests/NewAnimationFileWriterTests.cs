using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NewAnimationFileWriterTests
{
    [Fact]
    public void WriteEmpty_AchjFileName_WritesJsonWithPixelCoordinates()
    {
        using var stream = new MemoryStream();

        NewAnimationFileWriter.WriteEmpty(stream, "Player.achj");

        stream.Position = 0;
        // AnimationChainListSave defaults to UV; a new file written that way would make the very
        // next open prompt the user to convert to pixel coordinates (#1018).
        var reloaded = AnimationChainListSave.FromJsonStream(stream);
        Assert.Equal(TextureCoordinateType.Pixel, reloaded.CoordinateType);
        Assert.Empty(reloaded.AnimationChains);
    }

    [Fact]
    public void WriteEmpty_AchxFileName_WritesXml()
    {
        using var stream = new MemoryStream();

        NewAnimationFileWriter.WriteEmpty(stream, "Player.achx");

        stream.Position = 0;
        var text = new StreamReader(stream).ReadToEnd();
        Assert.StartsWith("<?xml", text);
    }
}
