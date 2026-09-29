using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #882: the add-frame cursor (Ctrl held over a loaded bitmap) must toggle the instant Ctrl is
/// pressed/released, even when the pointer never moves — not only on the next pointer move.
/// </summary>
public class AddFrameCursorCtrlToggleTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int size = 32)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.Red);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void CtrlPressAndRelease_WithoutPointerMove_TogglesAddFrameCursorImmediately()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png");
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = window.FindControl<WireframeControl>("WireframeCtrl")
                       ?? throw new InvalidOperationException("WireframeCtrl not found");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            var localPoint = new Point(ctrl.Bounds.Width / 2, ctrl.Bounds.Height / 2);
            var windowPoint = ctrl.TranslatePoint(localPoint, window) ?? localPoint;

            window.MouseMove(windowPoint);
            Dispatcher.UIThread.RunJobs();
            Assert.False(ctrl.IsShowingAddFrameCursor, "Cursor should be plain before Ctrl is held.");

            window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctrl.IsShowingAddFrameCursor,
                "Pressing Ctrl without moving the pointer should show the add-frame cursor immediately (#882).");

            window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(ctrl.IsShowingAddFrameCursor,
                "Releasing Ctrl without moving the pointer should drop the add-frame cursor immediately (#882).");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void CtrlPress_WithoutPointerMove_ShowsGhostUnderPointer()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Larger than the wireframe so the pointer is always over texture pixels.
            var texPath = WriteSolidPng(dir, "large.png", size: 2048);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = window.FindControl<WireframeControl>("WireframeCtrl")
                       ?? throw new InvalidOperationException("WireframeCtrl not found");
            ctrl.LoadTexture(texPath);
            ctrl.SetCamera(0f, 0f, 1f); // screen pixels == texture pixels
            Dispatcher.UIThread.RunJobs();

            var localPoint = new Point(ctrl.Bounds.Width / 2, ctrl.Bounds.Height / 2);
            var windowPoint = ctrl.TranslatePoint(localPoint, window) ?? localPoint;
            window.MouseMove(windowPoint);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(ctrl.AddFrameGhost);

            window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            var ghost = ctrl.AddFrameGhost;

            Assert.NotNull(ghost);
            Assert.True(ghost.Value.Contains((float)localPoint.X, (float)localPoint.Y),
                $"Ghost {ghost} should surround the hovered point {localPoint}.");

            window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(ctrl.AddFrameGhost);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
