#if DEBUG
using System;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace AnimationEditor.Browser;

/// <summary>
/// #690 Phase 1 harness bridge (DEBUG only). Avalonia.Browser 12.0.1 does not expose
/// control ARIA to the DOM (Phase 0 spike: CDP AX tree is only the page title), so Playwright
/// cannot <c>getByRole</c>. This bridge uses the same <see cref="AutomationProperties.AutomationId"/>
/// values (B1) from managed code — click by id, dump undo Descriptions for A2 asserts.
/// Registers <c>globalThis.__aeUiAutomation</c> via <c>aeUiAutomation.js</c>. Not in Release;
/// never auto-run from a query string.
/// </summary>
internal static partial class BrowserUiAutomation
{
    private const string ModuleName = "aeUiAutomation.js";

    private static Control? _root;
    private static IUndoManager? _undo;

    public static async Task AttachAsync(Control root, IUndoManager undo)
    {
        _root = root;
        _undo = undo;
        await JSHost.ImportAsync(ModuleName, "../aeUiAutomation.js");
        Register(ClickByAutomationId, DumpUndoDescriptionsJson);
    }

    [JSImport("register", ModuleName)]
    private static partial void Register(
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Boolean>>] Func<string, bool> clickById,
        [JSMarshalAs<JSType.Function<JSType.String>>] Func<string> dumpUndoJson);

    private static bool ClickByAutomationId(string automationId)
    {
        if (_root is null || string.IsNullOrEmpty(automationId)) return false;
        var target = _root.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
        if (target is null) return false;

        switch (target)
        {
            case Button button:
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                return true;
            case MenuItem menuItem:
                menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                return true;
            case TabItem tabItem:
                if (tabItem.Parent is TabControl tabs)
                    tabs.SelectedItem = tabItem;
                return true;
            default:
                return false;
        }
    }

    private static string DumpUndoDescriptionsJson()
    {
        if (_undo is null) return "[]";
        // Manual JSON — Browser WASM trims reflection, so JsonSerializer.Serialize throws
        // JsonSerializerIsReflectionDisabled under the default publish/trim settings.
        var parts = _undo.UndoHistory.Select(EscapeJsonString).ToArray();
        return "[" + string.Join(",", parts) + "]";
    }

    private static string EscapeJsonString(IUndoableCommand command)
    {
        var s = command.Description ?? "";
        var escaped = s
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}
#endif
