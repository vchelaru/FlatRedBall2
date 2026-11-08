using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A closed window must become garbage. A <see cref="DispatcherTimer"/> is rooted by the dispatcher, so one left
/// running by a control keeps the whole window and its service graph alive (and ticking) after Detach to New
/// Window and close. On failure the message carries the reference path from a static root to the window.
/// </summary>
/// <remarks>
/// Landmines that look like leaks but are not: the compositor holds a closed window until its next render tick;
/// a finished async call's stack frames still hold what it touched, so check from a later continuation; Avalonia's
/// double-click timer holds a clicked window until it fires; a Debug JIT stack temp in the test frame holds
/// whatever the test frame touched, so the work lives in helper methods.
/// </remarks>
public class WindowLeakTests
{
    private static void Tick()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static string? _rootPath;

    private static void AssertCollected(WeakReference window)
    {
        for (int pass = 0; pass < 6 && window.IsAlive; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Tick();
        }
        window.IsAlive.ShouldBeFalse(_rootPath ?? "no path from a static root");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndClose(Func<Window> create)
    {
        Window window = create();
        window.Show();
        Tick();
        window.Close();
        Tick();
        _rootPath = RootFinder.FindPath(window);
        return new WeakReference(window);
    }

    [AvaloniaFact]
    public void ClosedMainWindow_IsCollected() =>
        AssertCollected(OpenAndClose(() => TestHelpers.BuildServices().CreateMainWindow()));

    [AvaloniaFact]
    public void ClosedDetachedWindow_IsCollected() =>
        AssertCollected(OpenAndClose(() =>
        {
            string root = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
            return App.CreateDetachedWindow(root, Path.Combine(root, "recovery.achx"));
        }));

    // Held in a static, not a local: an async method's locals live in a state machine that outlives the call.
    private static AnimationEditorHarness? _editor;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task OpenAchxSequenceAndPngTabs()
    {
        _editor = new AnimationEditorHarness();
        string png = _editor.WritePng("sheet.png", 64, 64);
        string achx = _editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string seqj = _editor.WriteSeqj("boss.seqj", "Idle");
        await _editor.OpenAsync(achx);
        _editor.ClickRow(_editor.ChainNamed("Walk"));
        _editor.Preview.Play();
        await _editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        await _editor.OpenAsync(seqj);
        await _editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        await _editor.OpenPngTabAsync(png);
    }

    // The sequence tab's file watchers and their flush timer run only while that tab is in front.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task OpenSequenceTabInFront()
    {
        _editor = new AnimationEditorHarness();
        string seqj = _editor.WriteSeqj("boss.seqj", "Idle");
        await _editor.OpenAsync(seqj);
        await _editor.WaitAsync(TimeSpan.FromMilliseconds(300));
    }

    [AvaloniaFact]
    public async Task ClosedWindowWithTheSequenceTabInFront_IsCollected()
    {
        await OpenSequenceTabInFront();
        await Task.Delay(300);
        AssertCollected(CloseEditor());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CloseEditor()
    {
        AnimationEditorHarness editor = _editor!;
        _editor = null;
        var window = new WeakReference(editor.Window);
        editor.Dispose();
        Tick();
        _rootPath = RootFinder.FindPath(editor.Window);
        return window;
    }

    [AvaloniaFact]
    public async Task ClosedWindowWithPlayingAchxSequenceAndPngTabs_IsCollected()
    {
        await OpenAchxSequenceAndPngTabs();
        await Task.Delay(700);
        AssertCollected(CloseEditor());
    }
}
