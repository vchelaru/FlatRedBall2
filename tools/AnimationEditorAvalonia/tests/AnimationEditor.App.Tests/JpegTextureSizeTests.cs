using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>The editor reads the size of an image that is not a PNG from its header through Skia (#1952).</summary>
public class JpegTextureSizeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public JpegTextureSizeTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    // The second folder is past the classic 260-character Windows path limit, which Skia's own file open cannot read.
    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(300)]
    public async Task ApplyPickedTextureAsync_JpegOnAFrameWithNoRegion_ShowsTheWholeImage(int folderLength)
    {
        var dir = _dir;
        while (dir.Length < folderLength) dir = Path.Combine(dir, "level-" + new string('x', 20));
        Directory.CreateDirectory(dir);
        var jpg = Path.Combine(dir, "pic.jpg");
        using (var bitmap = new SKBitmap(48, 32))
        {
            bitmap.Erase(SKColors.Green);
            using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
            File.WriteAllBytes(jpg, data.ToArray());
        }
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "", FrameLength = 0.1f };
            chain.Frames.Add(frame);
            list.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = list;
            ctx.ProjectManager.FileName = Path.Combine(dir, "hero.achx");
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            await window.ApplyPickedTextureAsync(jpg);
            Dispatcher.UIThread.RunJobs();

            frame.TextureName.ShouldBe("pic.jpg");
            (frame.LeftCoordinate, frame.TopCoordinate, frame.RightCoordinate, frame.BottomCoordinate).ShouldBe((0f, 0f, 48f, 32f));
            // A file that is not an image has no size, so nothing to show whole.
            File.WriteAllText(Path.Combine(dir, "notes.jpg"), "not an image");
            ctx.AppCommands.GetTextureSize("notes.jpg").ShouldBeNull();
            ctx.AppCommands.GetTextureSize("gone.jpg").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
