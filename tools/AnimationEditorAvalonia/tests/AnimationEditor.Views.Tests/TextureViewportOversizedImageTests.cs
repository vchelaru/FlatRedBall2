using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// An image too big to decode used to throw out of LoadTextureAsync (or stay unloaded with no
// message); the viewport now reports why through TextureLoadError (#1552).
public class TextureViewportOversizedImageTests
{
    [AvaloniaFact]
    public void LoadTexture_OversizedImage_RaisesTextureLoadErrorAndStaysUnloaded()
    {
        string path = PngHeaderFile.Write("huge.png", 24000, 24000);
        try
        {
            var viewport = new TextureViewport();
            var errors = new List<string>();
            viewport.TextureLoadError += errors.Add;

            bool loaded = viewport.LoadTexture(path);

            Assert.False(loaded);
            Assert.Equal((0, 0), viewport.BitmapSize);
            string error = Assert.Single(errors);
            Assert.Contains("24000 x 24000", error);
        }
        finally
        {
            PngHeaderFile.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task LoadTextureAsync_OversizedImage_RaisesTextureLoadErrorWithoutThrowing()
    {
        string path = PngHeaderFile.Write("huge.png", 24000, 24000);
        try
        {
            var viewport = new TextureViewport();
            var errors = new List<string>();
            viewport.TextureLoadError += errors.Add;

            bool loaded = await viewport.LoadTextureAsync(path);

            Assert.False(loaded);
            string error = Assert.Single(errors);
            Assert.Contains("huge.png", error);
        }
        finally
        {
            PngHeaderFile.Delete(path);
        }
    }
}
