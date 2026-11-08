using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AnimationEditor.App.Services;

/// <summary>
/// Logs every window the app opens and closes. A hidden modal dialog is a prime suspect for a process that will not exit,
/// and this is the one place that sees all dialogs (confirm, prompt, About, Settings, pickers' own windows) without each
/// call site opting in.
/// </summary>
internal static class WindowLog
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;

        Window.WindowOpenedEvent.AddClassHandler(typeof(Window),
            (sender, _) => { if (sender is Window w) AppLog.Info($"Window opened: {Describe(w)}"); });
        Window.WindowClosedEvent.AddClassHandler(typeof(Window),
            (sender, _) => { if (sender is Window w) AppLog.Info($"Window closed: {Describe(w)}"); });
    }

    // owner= marks a dialog (shown with an owner), which blocks that owner while it is open.
    private static string Describe(Window w) =>
        $"{w.GetType().Name} \"{w.Title}\" owner={(w.Owner as Window)?.GetType().Name ?? "none"}";
}
