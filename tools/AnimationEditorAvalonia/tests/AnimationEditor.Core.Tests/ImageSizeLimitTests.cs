using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ImageSizeLimitTests
{
    [Theory]
    [InlineData(8192, 8192)]
    [InlineData(16385, 16385)]   // past the GPU max texture size, still renders (#1553)
    [InlineData(23170, 23170)]   // 2,147,395,600 bytes, the largest square that decodes
    [InlineData(100000, 5000)]   // wide and short, 2 GB exactly would be 134217728 x 4
    public void Exceeds_DecodableSize_ReturnsFalse(int width, int height)
    {
        Assert.False(ImageSizeLimit.Exceeds(width, height));
    }

    [Theory]
    [InlineData(23171, 23171)]
    [InlineData(24000, 24000)]
    [InlineData(70000, 8000)]    // non-square, same byte count problem
    [InlineData(65535, 65535)]   // width * height * 4 overflows int
    public void Exceeds_OverTwoGigabytesDecoded_ReturnsTrue(int width, int height)
    {
        Assert.True(ImageSizeLimit.Exceeds(width, height));
    }

    [Fact]
    public void Describe_NamesFileSizeAndLimit()
    {
        string message = ImageSizeLimit.Describe("huge.png", 24000, 24000);

        Assert.Contains("huge.png", message);
        Assert.Contains("24000 x 24000", message);
        Assert.Contains("2 GB", message);
    }
}
