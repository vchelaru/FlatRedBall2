using System.Text.RegularExpressions;
using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Input;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Builds the macOS menu bar from a harness window.</summary>
internal static class NativeMenuTestSupport
{
    internal static NativeMenu Build(AnimationEditorHarness editor) =>
        editor.Window.CreateNativeMenu();

    /// <summary>The item at <paramref name="path"/> (headers, outermost first) in <paramref name="bar"/>.</summary>
    internal static NativeMenuItem Find(NativeMenu bar, params string[] path)
    {
        NativeMenu menu = bar;
        NativeMenuItem? item = null;
        foreach (string header in path)
        {
            item = menu.Items.OfType<NativeMenuItem>().Single(candidate => candidate.Header == header);
            if (header != path[^1]) menu = item.Menu!;
        }
        return item!;
    }

    /// <summary>Opens each menu on the way to the item (as AppKit does), then activates it.</summary>
    internal static void Activate(NativeMenu bar, params string[] path)
    {
        NativeMenu menu = bar;
        foreach (string header in path.SkipLast(1))
        {
            NativeMenuItem parent = menu.Items.OfType<NativeMenuItem>().Single(candidate => candidate.Header == header);
            menu = parent.Menu!;
            RaiseOpening(menu);
        }
        Find(bar, path).Command!.Execute(null);
    }

    /// <summary>
    /// Raises <see cref="NativeMenu.Opening"/> the way AppKit does when the user opens the menu. The bridge method
    /// that raises it is not public, so it is reached by reflection.
    /// </summary>
    internal static void RaiseOpening(NativeMenu menu) =>
        typeof(NativeMenu).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Single(method => method.Name.EndsWith("RaiseOpening")).Invoke(menu, null);
}

/// <summary>
/// Compares the in-window <see cref="Menu"/> with the native bar built from it, item by item: kind, header
/// (access-key underscores dropped), toggle type, checked and enabled state, and the gesture on the Mac command key.
/// Each difference is one readable line naming the menu path.
/// </summary>
internal static class NativeMenuParity
{
    internal static List<string> Differences(Menu source, NativeMenu native, CommandModifier command)
    {
        var differences = new List<string>();
        Compare("", source.Items.Cast<object?>().ToList(), native, command, differences);
        return differences;
    }

    private static void Compare(string path, List<object?> source, NativeMenu native, CommandModifier command, List<string> differences)
    {
        List<NativeMenuItemBase> actual = native.Items.ToList();
        for (int index = 0; index < Math.Max(source.Count, actual.Count); index++)
        {
            object? expected = index < source.Count ? source[index] : null;
            NativeMenuItemBase? found = index < actual.Count ? actual[index] : null;
            string label = expected is MenuItem menuItem ? Strip((string?)menuItem.Header) : "-";
            string where = $"{path}/{label}";
            if (expected is null) { differences.Add($"{where}: native bar has an extra item at {index}"); continue; }
            if (found is null) { differences.Add($"{where}: missing from the native bar"); continue; }

            if (expected is MenuItem item)
            {
                if (found is not NativeMenuItem nativeItem) { differences.Add($"{where}: native item is a separator"); continue; }
                if (nativeItem.Header != label) differences.Add($"{where}: header is '{nativeItem.Header}'");
                if (nativeItem.IsEnabled != item.IsEnabled) differences.Add($"{where}: enabled is {nativeItem.IsEnabled}, in-window {item.IsEnabled}");
                if (item.ToggleType != nativeItem.ToggleType) differences.Add($"{where}: toggle type is {nativeItem.ToggleType}");
                if (nativeItem.ToggleType != MenuItemToggleType.None && nativeItem.IsChecked != (item.IsChecked == true))
                    differences.Add($"{where}: checked is {nativeItem.IsChecked}, in-window {item.IsChecked}");
                if (nativeItem.Gesture != ExpectedGesture(item.InputGesture, command))
                    differences.Add($"{where}: gesture is {nativeItem.Gesture}, expected {ExpectedGesture(item.InputGesture, command)}");
                List<object?> children = item.Items.Cast<object?>().ToList();
                if (children.Count > 0)
                {
                    if (nativeItem.Menu is null) differences.Add($"{where}: native item has no submenu");
                    else Compare(where, children, nativeItem.Menu, command, differences);
                }
            }
            else if (found is not NativeMenuItemSeparator)
            {
                differences.Add($"{where}: native item is not a separator");
            }
        }
    }

    internal static KeyGesture? ExpectedGesture(KeyGesture? gesture, CommandModifier command)
    {
        if (gesture is null) return null;
        KeyModifiers modifiers = gesture.KeyModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control))
            modifiers = (modifiers & ~KeyModifiers.Control) | command.Modifier;
        return new KeyGesture(gesture.Key, modifiers);
    }

    private static string Strip(string? header) => Regex.Replace(header ?? "", "_(.)", "$1");
}
