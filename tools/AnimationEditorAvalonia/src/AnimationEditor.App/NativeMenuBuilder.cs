using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnimationEditor.App;

/// <summary>
/// Builds the macOS system menu bar as a live mirror of the in-window <see cref="Menu"/>, so the two have one
/// definition: a new item, header, gesture, checked state or enabled state in the in-window menu shows up in the bar
/// with no second list to update. Kept apart from <see cref="App"/> so headless tests can inspect the result.
/// </summary>
/// <remarks>
/// Gestures are copied from the in-window items (which take them from the hotkey registry) with Ctrl moved to the Mac
/// command key. Activating a native item raises Click on the in-window item, so both obey the same enabled state and
/// run the same handler. Opening a native submenu raises <see cref="MenuItem.SubmenuOpenedEvent"/> on its in-window
/// item, so handlers that refresh state when the menu opens (Close Project Folder) run for the bar too.
/// </remarks>
internal static class NativeMenuBuilder
{
    internal static NativeMenu Build(Menu source)
    {
        var bar = new NativeMenu();
        _ = new MenuMirror(bar, source.Items);   // kept alive by its subscriptions on the in-window items
        return bar;
    }

    private static string HeaderText(MenuItem item) =>
        Regex.Replace(item.Header as string ?? item.Header?.ToString() ?? "", "_(.)", "$1");

    /// <summary>The in-window gesture with Ctrl moved to the Mac command key.</summary>
    internal static KeyGesture? ToMacGesture(KeyGesture? gesture)
    {
        if (gesture is null) return null;
        KeyModifiers modifiers = gesture.KeyModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control))
            modifiers = (modifiers & ~KeyModifiers.Control) | KeyModifiers.Meta;
        return new KeyGesture(gesture.Key, modifiers);
    }

    /// <summary>Runs an item's action; can execute only while the in-window item is enabled, which is how a native item greys out.</summary>
    private sealed class ActivateCommand(MenuItem source, Action execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => source.IsEnabled;
        public void Execute(object? parameter) => execute();
        internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>One native menu kept equal to one in-window item collection; rebuilt when the collection changes.</summary>
    private sealed class MenuMirror : IDisposable
    {
        private readonly NativeMenu _native;
        private readonly ItemCollection _source;
        private readonly List<ItemMirror> _children = new();

        internal MenuMirror(NativeMenu native, ItemCollection source)
        {
            _native = native;
            _source = source;
            ((INotifyCollectionChanged)_source).CollectionChanged += OnSourceChanged;
            Rebuild();
        }

        private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

        private void Rebuild()
        {
            DisposeChildren();
            _native.Items.Clear();
            foreach (object? child in _source)
            {
                if (child is MenuItem item)
                {
                    var mirror = new ItemMirror(item, Rebuild);
                    _children.Add(mirror);
                    _native.Add(mirror.Native);
                }
                else if (child is Separator)
                {
                    _native.Add(new NativeMenuItemSeparator());
                }
            }
        }

        private void DisposeChildren()
        {
            foreach (ItemMirror child in _children) child.Dispose();
            _children.Clear();
        }

        public void Dispose()
        {
            ((INotifyCollectionChanged)_source).CollectionChanged -= OnSourceChanged;
            DisposeChildren();
        }
    }

    /// <summary>One native item kept equal to one in-window item.</summary>
    /// <remarks>
    /// An item is built either as a plain item (a command) or as a submenu, from whether the in-window item has children,
    /// and never edited between the two: Avalonia's macOS exporter keeps one native submenu per native item and throws
    /// (crashing the process) if that item's <see cref="NativeMenuItem.Menu"/> is later swapped or removed. When the
    /// in-window item crosses between "no children" and "children" (File > Open Recent on every file opened), the parent
    /// menu replaces the whole native item instead.
    /// </remarks>
    private sealed class ItemMirror : IDisposable
    {
        private readonly MenuItem _source;
        private readonly ActivateCommand _activate;
        private readonly Action _replaceMe;
        private readonly bool _isSubmenu;
        private MenuMirror? _submenu;
        private NativeMenu? _menu;

        internal NativeMenuItem Native { get; } = new();

        internal ItemMirror(MenuItem source, Action replaceMe)
        {
            _source = source;
            _replaceMe = replaceMe;
            _activate = new ActivateCommand(_source, Activate);
            _source.PropertyChanged += OnSourcePropertyChanged;
            ((INotifyCollectionChanged)_source.Items).CollectionChanged += OnItemsChanged;
            _isSubmenu = _source.Items.Count > 0;
            // Before CopyState: setting a command makes the native item recompute its enabled state.
            if (_isSubmenu)
            {
                _menu = new NativeMenu();
                _menu.Opening += OnSubmenuOpening;
                _submenu = new MenuMirror(_menu, _source.Items);
                Native.Menu = _menu;
            }
            else
            {
                Native.Command = _activate;
            }
            CopyState();
        }

        private void OnSourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == MenuItem.HeaderProperty || e.Property == InputElement.IsEnabledProperty
                || e.Property == MenuItem.IsCheckedProperty || e.Property == MenuItem.InputGestureProperty
                || e.Property == MenuItem.ToggleTypeProperty)
                CopyState();
        }

        private void CopyState()
        {
            Native.Header = HeaderText(_source);
            Native.IsEnabled = _source.IsEnabled;
            _activate.RaiseCanExecuteChanged();
            Native.Gesture = ToMacGesture(_source.InputGesture);
            Native.ToggleType = _source.ToggleType;
            Native.IsChecked = _source.IsChecked;
        }

        // An item with children is a submenu and has no action of its own; one that is empty (Open Recent before any
        // file was opened) is a plain item until children arrive.
        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if ((_source.Items.Count > 0) != _isSubmenu) _replaceMe();
        }

        private void OnSubmenuOpening(object? sender, EventArgs e)
        {
            _source.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
        }

        private void Activate()
        {
            if (!_source.IsEnabled) return;
            // A real click flips the checkmark before Click fires; a raised Click alone does not.
            if (_source.ToggleType != MenuItemToggleType.None)
                _source.IsChecked = !_source.IsChecked;
            _source.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Native.IsChecked = _source.IsChecked;   // AppKit may have flipped its own checkmark
        }

        public void Dispose()
        {
            _source.PropertyChanged -= OnSourcePropertyChanged;
            ((INotifyCollectionChanged)_source.Items).CollectionChanged -= OnItemsChanged;
            if (_menu is not null) _menu.Opening -= OnSubmenuOpening;
            _submenu?.Dispose();
        }
    }
}
