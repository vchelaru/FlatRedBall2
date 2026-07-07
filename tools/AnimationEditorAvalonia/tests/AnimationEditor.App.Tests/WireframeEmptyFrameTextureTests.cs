using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A frame with no texture must show a blank wireframe, not borrow a sibling frame's texture. The
/// "borrow the first project texture" fallback (#618) is scoped to when no frame is selected (an empty
/// chain), so it still seeds a Ctrl-clickable canvas there without misleading a genuinely empty frame.
/// </summary>
public class WireframeEmptyFrameTextureTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int size)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.Blue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    private static AnimationFrameSave Frame(string textureName) => new()
    {
        TextureName      = textureName,
        RightCoordinate  = 1f, BottomCoordinate = 1f,
        FrameLength      = 0.1f,
        ShapesSave       = new ShapesSave(),
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A selected empty chain (no frame) still borrows the first project texture, so #618's
    /// "show something Ctrl-clickable to seed the first frame" behavior is preserved.
    /// </summary>
    [AvaloniaFact]
    public void DetermineTexturePath_EmptyChainSelectedNoFrame_BorrowsFirstProjectTexture()
    {
        var ctx = ResetSingletons();
        var acls = new AnimationChainListSave();
        var textured = new AnimationChainSave { Name = "Has" };
        textured.Frames.Add(Frame(@"C:\sheets\hero.png"));
        var emptyChain = new AnimationChainSave { Name = "Empty" };   // no frames
        acls.AnimationChains.Add(textured);
        acls.AnimationChains.Add(emptyChain);
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;   // TextureName is absolute, returned verbatim

        var ctrl = ctx.CreateWireframeControl();
        ctx.SelectedState.SelectedChain = emptyChain;   // a chain, but no frame

        Assert.Equal(@"C:\sheets\hero.png", ctrl.DetermineTexturePath());
    }

    /// <summary>
    /// A selected frame with no texture stays blank even when a sibling frame in the same chain
    /// carries one — it must not borrow the sibling's (or any project) texture.
    /// </summary>
    [AvaloniaFact]
    public void DetermineTexturePath_SelectedEmptyFrameWithTexturedSibling_ReturnsNull()
    {
        var ctx = ResetSingletons();
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame(@"C:\sheets\hero.png"));   // frame 1: textured
        var empty = Frame("");                            // frame 2: no texture
        chain.Frames.Add(empty);
        acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;

        var ctrl = ctx.CreateWireframeControl();
        ctx.SelectedState.SelectedFrame = empty;

        Assert.Null(ctrl.DetermineTexturePath());
    }

    /// <summary>
    /// End-to-end in a saved project: selecting an empty frame after a textured one blanks the
    /// wireframe canvas and clears the texture combo, instead of leaving the sibling's texture showing.
    /// </summary>
    [AvaloniaFact]
    public void SelectingEmptyFrame_AfterTexturedSibling_BlanksWireframeAndClearsCombo()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(dir, "sheet.png", 128);
            var acls = new AnimationChainListSave();
            var chain = new AnimationChainSave { Name = "Run" };
            var textured = Frame("sheet.png");   // relative to the .achx folder
            var empty    = Frame("");
            chain.Frames.Add(textured);
            chain.Frames.Add(empty);
            acls.AnimationChains.Add(chain);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // A saved project (FileName set) is what populates the texture combo, so this is the case
            // where SyncTextureCombo — not just DetermineTexturePath — would re-borrow the texture.
            ctx.ProjectManager.FileName = Path.Combine(dir, "anim.achx");
            ctx.ProjectManager.AnimationChainListSave = acls;
            typeof(MainWindow).GetMethod("RefreshTextureCombo", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);

            var ctrl  = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var combo = FindCtrl<ComboBox>(window, "TextureCombo");

            // Select the textured frame first: the sheet loads and the combo points at it.
            ctx.SelectedState.SelectedFrame = textured;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((128, 128), ctrl.BitmapSize);

            // Now the empty frame: canvas must blank and combo must clear.
            ctx.SelectedState.SelectedFrame = empty;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal((0, 0), ctrl.BitmapSize);
            Assert.Null(combo.SelectedItem);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Clearing the texture-name field and committing (ENTER / blur) clears the frame's texture and
    /// blanks the wireframe, instead of being ignored so the old texture keeps showing.
    /// </summary>
    [AvaloniaFact]
    public void ApplyTextureName_ClearedField_ClearsFrameTextureAndBlanksWireframe()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "sheet.png", 128);
            var acls = new AnimationChainListSave();
            var chain = new AnimationChainSave { Name = "Run" };
            var frame = Frame(texPath);   // absolute path (unsaved project)
            chain.Frames.Add(frame);
            acls.AnimationChains.Add(chain);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ctx.ProjectManager.FileName = null;
            ctx.ProjectManager.AnimationChainListSave = acls;

            var ctrl    = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var nameBox = FindCtrl<TextBox>(window, "PropTextureName");

            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((128, 128), ctrl.BitmapSize);

            // Clear the field and commit — mirrors deleting the text and pressing ENTER.
            nameBox.Text = "";
            typeof(MainWindow).GetMethod("ApplyTextureName", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(string.IsNullOrEmpty(frame.TextureName), "Clearing the field should clear the frame's texture.");
            Assert.Equal((0, 0), ctrl.BitmapSize);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
