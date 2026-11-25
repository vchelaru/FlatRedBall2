using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A texture that was half written or missing, and then is there.</summary>
public class TextureAppearsScenarioTests
{
    [AvaloniaFact]
    public async Task PngHalfWritten_ThenDone_ShowsTheFinishedImage_AndKeepsTheRegions()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        byte[] finished = File.ReadAllBytes(editor.WritePng("other.png", 128, 32));

        File.WriteAllBytes(png, finished[..40]);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(800));
        editor.Services.AppCommands.SetFramePixelRegion(editor.ChainNamed("Walk").Frames, pixelX: 4, pixelY: null, pixelW: null, pixelH: null);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        File.WriteAllBytes(png, finished);
        await editor.WaitUntilAsync(() => editor.Wireframe.BitmapSize == (128, 32), TimeSpan.FromSeconds(3));

        editor.Wireframe.BitmapSize.ShouldBe((128, 32));
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(hero).AnimationChains.Single().Frames.Single();
        ((int)saved.LeftCoordinate, (int)saved.RightCoordinate).ShouldBe((4, 20));
    }

    // Nothing is selected, so no frame says which texture the canvas shows.
    [AvaloniaFact]
    public async Task TilesetImageHalfWritten_ThenDone_WithNoAnimations_ShowsTheFinishedImage()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string tsx = Path.Combine(editor.ProjectFolder, "tiles.tsx");
        File.WriteAllText(tsx, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="tiles" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="sheet.png" width="64" height="64"/>
            </tileset>
            """);
        await editor.OpenAsync(tsx);
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        byte[] finished = File.ReadAllBytes(editor.WritePng("other.png", 128, 32));

        File.WriteAllBytes(png, finished[..40]);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(800));
        editor.Wireframe.RefreshAll();
        File.WriteAllBytes(png, finished);
        await editor.WaitUntilAsync(() => editor.Wireframe.BitmapSize == (128, 32), TimeSpan.FromSeconds(3));

        editor.Wireframe.BitmapSize.ShouldBe((128, 32));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PngUnreadableForAMoment_ThenDone_ShowsTheFinishedImage(bool locked)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)));
        await editor.OpenAsync(hero);
        byte[] finished = File.ReadAllBytes(editor.WritePng("other.png", 128, 32));

        // Locked: the finished bytes are all there, but the writer holds the file a while longer.
        using (FileStream writing = new FileStream(png, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (locked) writing.Write(finished);
            writing.Flush(true);
            if (locked) await editor.WaitAsync(TimeSpan.FromMilliseconds(800));
        }
        if (!locked)
        {
            await editor.WaitAsync(TimeSpan.FromMilliseconds(800));
            editor.Wireframe.BitmapSize.ShouldBe((0, 0));
            File.WriteAllBytes(png, finished);
        }
        await editor.WaitUntilAsync(() => editor.Wireframe.BitmapSize == (128, 32), TimeSpan.FromSeconds(3));

        editor.Wireframe.BitmapSize.ShouldBe((128, 32));
    }

    // Nothing is left to inherit a frame from, and the canvas shows no image to take the whole of.
    [AvaloniaFact]
    public async Task AddFrame_WhileTheCanvasTextureIsMissing_DoesNotBecomeOnePixelOfIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)));
        await editor.OpenAsync(hero);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Services.AppCommands.DeleteFrames(walk.Frames.ToList());
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        editor.Services.AppCommands.AddFrame(walk);
        editor.WritePng("sheet.png", 64, 64);
        editor.Services.AppCommands.SetFrameTextureName([walk.Frames[0]], "sheet.png");

        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(hero).AnimationChains.Single().Frames.Single();
        (saved.LeftCoordinate, saved.TopCoordinate, saved.RightCoordinate, saved.BottomCoordinate).ShouldBe((0f, 0f, 64f, 64f));
    }

    [AvaloniaTheory]
    [InlineData("art")]
    [InlineData("")]
    public async Task MissingTexture_ThatAppearsLater_IsShown(string folder)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string texture = Path.Combine(folder, "sheet.png").Replace('\\', '/');
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", texture, (8, 8, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, folder));
        File.Move(editor.WritePng("new.png", 64, 64), Path.Combine(editor.ProjectFolder, texture));

        await editor.WaitUntilAsync(() => editor.Wireframe.BitmapSize == (64, 64), TimeSpan.FromSeconds(2));

        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        var shown = editor.Wireframe.CameraState;
        editor.Wireframe.CameraMemory = new();
        editor.Wireframe.RefreshAll();
        shown.ShouldBe(editor.Wireframe.CameraState, "centered, as a texture that is there when the file opens");
    }
}
