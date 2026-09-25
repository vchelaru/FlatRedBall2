using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The real <see cref="MainWindow"/> on a fresh service graph, driven the way a user drives it:
/// pointer and key input into a headless window (nothing reaches the desktop), dialogs answered
/// by a script, and results read back from the tree, the controls, the undo history, the
/// notification overlay and the saved <c>.achx</c>. Projects live in a temp folder that goes
/// away on dispose; settings go to the graph's own temp root, never the developer's.
/// </summary>
internal sealed class AnimationEditorHarness : IDisposable
{
    /// <param name="settingsRoot">
    /// Where the editor's per-user settings go for this run; a second harness given the same
    /// root starts the way a restarted editor would. Defaults to a fresh temp folder.
    /// </param>
    /// <param name="recoveryFilePath">
    /// Where the crash-recovery file for an unsaved document lives; give a second harness the
    /// file a first one left behind to start the way the editor does after a crash. Defaults to
    /// a fresh temp file.
    /// </param>
    public AnimationEditorHarness(string? settingsRoot = null, string? recoveryFilePath = null)
    {
        Services = settingsRoot is null ? new TestServices() : new TestServices { SettingsRoot = settingsRoot };
        if (recoveryFilePath != null)
        {
            Services.IoManager.RecoveryFilePath = recoveryFilePath;
        }
        Services.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        Services.ProjectManager.FileName = null;
        ProjectFolder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ProjectFolder);

        Dialogs = new ScriptedDialogs();
        Services.EditorDialogHost = Dialogs;
        Window = Services.CreateMainWindow();
        Window.Width = 1280;
        Window.Height = 800;
        Window.Show();
        Layout();

        // The window's constructor wires the production dialogs onto these seams, so the script
        // has to go on after construction or it is silently replaced.
        Services.AppCommands.ConfirmAsync = Dialogs.ConfirmAsync;
        Services.AppCommands.PromptStringAsync = Dialogs.PromptStringAsync;
        Services.AppCommands.FileDialogService = Dialogs;
        Window.ShowSaveDiscardCancelDialogAsync = Dialogs.SaveDiscardCancelAsync;
    }

    public TestServices Services { get; }

    public MainWindow Window { get; }

    /// <summary>The scripted dialogs; queue an answer before the gesture that opens one.</summary>
    public ScriptedDialogs Dialogs { get; }

    /// <summary>The temp folder fixture files are written to and projects are opened from.</summary>
    public string ProjectFolder { get; }

    /// <summary>The project the editor currently edits.</summary>
    public AnimationChainListSave Project =>
        Services.ProjectManager.AnimationChainListSave ?? throw new InvalidOperationException("The editor has no project.");

    public UndoManager UndoManager => Services.UndoManager;

    /// <summary>Undo history descriptions, most recent first.</summary>
    public List<string> UndoLabels => UndoManager.UndoHistory.Select(command => command.Description).ToList();

    public TreeView AnimTree => Control<TreeView>("AnimTree");

    public WireframeControl Wireframe => Control<WireframeControl>("WireframeCtrl");

    public PreviewControl Preview => Control<PreviewControl>("PreviewCtrl");

    public EditorNotificationOverlay Notifications => Control<EditorNotificationOverlay>("Notifications");

    /// <summary>The error banner's text while it shows, else null.</summary>
    public string? ErrorBannerText => Notifications.ErrorBanner.IsVisible ? Notifications.ErrorBannerText.Text : null;

    /// <summary>The toast's text while it shows, else null.</summary>
    public string? ToastText => Notifications.ToastPanel.IsVisible ? Notifications.ToastMessage.Text : null;

    /// <summary>The "… deleted" toast's text while it shows, else null.</summary>
    public string? DeletedToastText => Notifications.ItemDeletedToastPanel.IsVisible ? Notifications.ItemDeletedToastLabel.Text : null;

    /// <summary>The window title's file part, as the title bar shows it.</summary>
    public string TitleFileName => Control<TextBlock>("TitleFileName").Text ?? "";

    public TabManager Tabs =>
        (TabManager)typeof(MainWindow).GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(Window)!;

    /// <summary>
    /// Chain headers the tree shows after the search filter. A filtered-out row is hidden, not
    /// removed, and a selected row stays visible whatever the filter says (the row binds
    /// IsVisible to PinnedVisible || IsSelected), so Delete never acts on something unseen.
    /// </summary>
    public List<string> VisibleChainHeaders =>
        Nodes.Where(node => node.IsChainNode && (node.PinnedVisible || AnimTree.SelectedItems.Contains(node)))
            .Select(node => node.Header).ToList();

    /// <summary>Every node in the tree, depth first.</summary>
    public IEnumerable<TreeNodeVm> Nodes => Flatten(AnimTree.ItemsSource?.OfType<TreeNodeVm>() ?? Enumerable.Empty<TreeNodeVm>());

    public T Control<T>(string name) where T : Control =>
        Window.FindControl<T>(name) ?? throw new InvalidOperationException($"The window has no control named {name}.");

    #region Project fixtures

    /// <summary>Writes a solid PNG into <see cref="ProjectFolder"/> and returns its path.</summary>
    public string WritePng(string name, int width, int height, SKColor? color = null)
    {
        string path = Path.Combine(ProjectFolder, name);
        using SKBitmap bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? SKColors.CornflowerBlue);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>Writes a transparent PNG with one opaque rectangle (x, y, width, height), for the magic wand.</summary>
    public string WritePngWithBlob(string name, int width, int height, (int X, int Y, int Width, int Height) blob)
    {
        string path = Path.Combine(ProjectFolder, name);
        using SKBitmap bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        using (SKCanvas canvas = new SKCanvas(bitmap))
        using (SKPaint paint = new SKPaint { Color = SKColors.OrangeRed })
        {
            canvas.DrawRect(new SKRect(blob.X, blob.Y, blob.X + blob.Width, blob.Y + blob.Height), paint);
        }
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Writes a pixel-coordinate <c>.achx</c> into <see cref="ProjectFolder"/> and returns its path.
    /// Pixel coordinates keep the real open path from asking about a UV conversion.
    /// </summary>
    public string WriteAchx(string name, params AnimationChainSave[] chains)
    {
        string path = Path.Combine(ProjectFolder, name);
        AnimationChainListSave list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.AddRange(chains);
        list.Save(path);
        return path;
    }

    /// <summary>
    /// A chain whose frames are pixel rectangles (x, y, width, height) on <paramref name="texture"/>,
    /// for <see cref="WriteAchx"/>. Once opened, the editor holds the coordinates as UV (0..1) in
    /// memory and converts back to pixels when it saves.
    /// </summary>
    public static AnimationChainSave Chain(string name, string texture, params (int X, int Y, int Width, int Height)[] frames)
    {
        AnimationChainSave chain = new AnimationChainSave { Name = name };
        foreach ((int x, int y, int width, int height) in frames)
        {
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = texture,
                FrameLength = 0.1f,
                LeftCoordinate = x,
                TopCoordinate = y,
                RightCoordinate = x + width,
                BottomCoordinate = y + height,
            });
        }
        return chain;
    }

    /// <summary>Opens <paramref name="path"/> through the editor's real load path, as File &gt; Open would.</summary>
    public async Task OpenAsync(string path)
    {
        await Window.OpenFileAsTab(path);
        Layout();
        Dialogs.ThrowIfUnanswered();
    }

    /// <summary>Reads a saved project back from disk.</summary>
    public static AnimationChainListSave ReadSaved(string path) => AnimationChainListSave.FromFile(path);

    /// <summary>
    /// The loaded chain named <paramref name="name"/>. Opening a file loads fresh objects, so a
    /// fixture's own instances are not the ones the tree shows; look the loaded ones up here.
    /// </summary>
    public AnimationChainSave ChainNamed(string name) =>
        Project.AnimationChains.FirstOrDefault(chain => chain.Name == name)
        ?? throw new InvalidOperationException($"The project has no chain named {name}; it has [{string.Join(", ", Project.AnimationChains.Select(chain => chain.Name))}].");

    #endregion

    #region Tree

    public TreeNodeVm NodeFor(object data) =>
        Nodes.FirstOrDefault(node => ReferenceEquals(node.Data, data))
        ?? throw new InvalidOperationException($"The tree has no node for {Describe(data)}; it shows [{string.Join(", ", Nodes.Select(node => node.Header))}].");

    /// <summary>
    /// The realized row showing <paramref name="data"/>, scrolled into view first the way a user
    /// would scroll to it; its parents must be expanded.
    /// </summary>
    public TreeViewItem RowFor(object data)
    {
        Layout();
        TreeViewItem row = AnimTree.GetVisualDescendants().OfType<TreeViewItem>()
            .FirstOrDefault(candidate => candidate.DataContext is TreeNodeVm node && ReferenceEquals(node.Data, data))
            ?? throw new InvalidOperationException($"No tree row is realized for {Describe(data)}; expand its parent first. The tree shows [{string.Join(", ", Nodes.Select(node => node.Header))}] and {(Nodes.Any(node => ReferenceEquals(node.Data, data)) ? "has" : "has no")} node for it.");
        // TreeView.ScrollIntoView only knows its top-level items: a frame row a hundred rows down
        // a long chain stayed off screen and the click landed on nothing (the chain kept the
        // selection, so Delete removed the chain). Ask the row itself to scroll into the viewport.
        row.BringIntoView();
        Layout();
        return row;
    }

    /// <summary>
    /// The middle of the row's header label, which is inside the row's own header even when the
    /// row is expanded and its bounds span the children too.
    /// </summary>
    public Point RowHeaderPoint(object data)
    {
        TreeViewItem row = RowFor(data);
        TextBlock label = row.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(block => block.Name == "RowHeaderLabel" && ReferenceEquals(block.DataContext, row.DataContext))
            ?? throw new InvalidOperationException($"The row for {Describe(data)} has no header label.");
        Point point = CenterOf(label);
        Rect viewport = new Rect(PointIn(AnimTree, 0, 0), AnimTree.Bounds.Size);
        if (!viewport.Contains(point))
        {
            throw new InvalidOperationException($"The row for {Describe(data)} is at {point}, outside the tree's viewport {viewport}; a click there would land on nothing.");
        }
        return point;
    }

    public void ClickRow(object data, RawInputModifiers modifiers = RawInputModifiers.None) => ClickAt(RowHeaderPoint(data), modifiers);

    public void DoubleClickRow(object data) => DoubleClickAt(RowHeaderPoint(data));

    public void RightClickRow(object data) => RightClickAt(RowHeaderPoint(data));

    /// <summary>Expands the row for <paramref name="data"/> by clicking its chevron.</summary>
    public void Expand(object data)
    {
        TreeViewItem row = RowFor(data);
        if (row.IsExpanded)
        {
            return;
        }
        ToggleButton? chevron = row.GetVisualDescendants().OfType<ToggleButton>()
            .FirstOrDefault(button => button.TemplatedParent == row);
        if (chevron is { IsEffectivelyVisible: true } && chevron.Bounds.Width > 0)
        {
            Click(chevron);
        }
        if (!row.IsExpanded)
        {
            throw new InvalidOperationException($"Clicking the chevron did not expand the row for {Describe(data)}.");
        }
    }

    /// <summary>The context menu the last right-click on the tree opened.</summary>
    public ContextMenu TreeMenu =>
        AnimTree.ContextMenu is { IsOpen: true } menu ? menu : throw new InvalidOperationException("The tree's context menu is not open.");

    /// <summary>Headers of the open tree context menu, top level only, separators left out.</summary>
    public List<string> TreeMenuHeaders => TreeMenu.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ToList();

    /// <summary>
    /// Picks an item from the open tree context menu by its header, walking submenus for a longer
    /// <paramref name="path"/> ("Duplicate", "Flip Horizontal"); a click when the popup laid the
    /// item out, else the item's own click event.
    /// </summary>
    public void PickTreeMenuItem(params string[] path) => PickMenuItem(TreeMenu, path);

    /// <summary><see cref="PickTreeMenuItem"/> for any open context menu, e.g. the Project panel's.</summary>
    public void PickMenuItem(ContextMenu menu, params string[] path)
    {
        IEnumerable<object?> items = menu.Items;
        MenuItem? item = null;
        foreach (string header in path)
        {
            item = items.OfType<MenuItem>().FirstOrDefault(candidate => (string?)candidate.Header == header)
                ?? throw new InvalidOperationException($"The menu has no item \"{header}\"; it shows [{string.Join(", ", items.OfType<MenuItem>().Select(candidate => candidate.Header))}].");
            items = item.Items;
        }
        if (item!.IsEffectivelyVisible && item.Bounds.Width > 0)
        {
            Click(item);
        }
        else
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        menu.Close();
        Layout();
    }

    /// <summary>A button in the row for <paramref name="data"/> by its tooltip ("Add Frame", "Lock Animation").</summary>
    public Button RowButton(object data, string tooltip)
    {
        TreeViewItem row = RowFor(data);
        return row.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => ToolTip.GetTip(button) as string == tooltip && ReferenceEquals(button.DataContext, row.DataContext))
            ?? throw new InvalidOperationException($"The row for {Describe(data)} has no \"{tooltip}\" button.");
    }

    /// <summary>The inline rename box in the row for <paramref name="data"/>, once it is editing.</summary>
    public TextBox InlineRenameBox(object data)
    {
        TreeViewItem row = RowFor(data);
        return row.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(box => ReferenceEquals(box.DataContext, row.DataContext) && box.IsEffectivelyVisible)
            ?? throw new InvalidOperationException($"The row for {Describe(data)} is not editing its name.");
    }

    /// <summary>Rows of the History panel, oldest first, as the panel shows them.</summary>
    public List<string> HistoryRows =>
        (Control<ItemsControl>("HistoryList").ItemsSource?.OfType<HistoryEntryVm>() ?? Enumerable.Empty<HistoryEntryVm>())
        .Select(entry => entry.Description).ToList();

    #endregion

    #region Tabs

    /// <summary>The tab strip entry labelled <paramref name="displayName"/>.</summary>
    public Border TabFor(string displayName)
    {
        Layout();
        StackPanel strip = Control<StackPanel>("TabStrip");
        return strip.Children.OfType<Border>()
            .FirstOrDefault(border => border.GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == displayName))
            ?? throw new InvalidOperationException($"No tab is labelled {displayName}; the strip shows [{string.Join(", ", TabLabels)}].");
    }

    public List<string> TabLabels =>
        Control<StackPanel>("TabStrip").Children.OfType<Border>()
            .Select(border => border.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text ?? "")
            .ToList();

    public void ClickTab(string displayName) =>
        Click(TabFor(displayName).GetVisualDescendants().OfType<TextBlock>().First(label => label.Text == displayName));

    /// <summary>Clicks the ✕ on the tab labelled <paramref name="displayName"/>.</summary>
    public void CloseTab(string displayName) =>
        Click(TabFor(displayName).GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "✕"));

    #endregion

    #region Wireframe

    /// <summary>Where <paramref name="frame"/>'s box is drawn on the wireframe, in window coordinates.</summary>
    public Rect WireframeRectOf(AnimationFrameSave frame)
    {
        Layout();
        (int width, int height) = Wireframe.BitmapSize;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The wireframe has no texture loaded.");
        }
        (float panX, float panY, float zoom) = Wireframe.CameraState;
        (float left, float top, float right, float bottom) = CanvasTransform.TextureRectToScreen(
            frame.LeftCoordinate * width, frame.TopCoordinate * height,
            frame.RightCoordinate * width, frame.BottomCoordinate * height, panX, panY, zoom);
        Point origin = PointIn(Wireframe, 0, 0);
        return new Rect(origin.X + left, origin.Y + top, right - left, bottom - top);
    }

    /// <summary>The window point over texture pixel (<paramref name="x"/>, <paramref name="y"/>) on the wireframe.</summary>
    public Point WireframePointAt(float x, float y)
    {
        Layout();
        (float panX, float panY, float zoom) = Wireframe.CameraState;
        (float left, float top, _, _) = CanvasTransform.TextureRectToScreen(x, y, x, y, panX, panY, zoom);
        return PointIn(Wireframe, left, top);
    }

    /// <summary>The pixel rectangle (x, y, width, height) <paramref name="frame"/> covers on its texture.</summary>
    public (int X, int Y, int Width, int Height) PixelRectOf(AnimationFrameSave frame)
    {
        (int width, int height) = Wireframe.BitmapSize;
        int left = (int)MathF.Round(frame.LeftCoordinate * width);
        int top = (int)MathF.Round(frame.TopCoordinate * height);
        int right = (int)MathF.Round(frame.RightCoordinate * width);
        int bottom = (int)MathF.Round(frame.BottomCoordinate * height);
        return (left, top, right - left, bottom - top);
    }

    #endregion

    #region Gestures

    /// <summary>
    /// Clicks a main-menu item by its x:Name ("MenuSave"): opens the menus above it and clicks
    /// the item where the popup laid it out, so toggling and enabling behave as for a user.
    /// </summary>
    public void ClickMenu(string name) => ClickMenu(Control<MenuItem>(name));

    /// <summary>Clicks <paramref name="item"/> the same way, for menu items built in code (recent files).</summary>
    public void ClickMenu(MenuItem item)
    {
        if (!item.IsEnabled)
        {
            throw new InvalidOperationException($"{item.Header} is disabled.");
        }
        List<MenuItem> ancestors = new List<MenuItem>();
        for (MenuItem? parent = item.Parent as MenuItem; parent != null; parent = parent.Parent as MenuItem)
        {
            ancestors.Insert(0, parent);
        }
        foreach (MenuItem ancestor in ancestors)
        {
            if (!ancestor.IsSubMenuOpen)
            {
                Click(ancestor);
            }
            if (!ancestor.IsSubMenuOpen)
            {
                ancestor.IsSubMenuOpen = true;
                Layout();
            }
        }
        if (item.IsEffectivelyVisible && item.Bounds.Width > 0)
        {
            Click(item);
        }
        else
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        foreach (MenuItem ancestor in ancestors)
        {
            ancestor.IsSubMenuOpen = false;
        }
        // Leave menu mode the way a click does on the desktop: while the root Menu stays open
        // it keeps the keyboard, so the next hotkey (Ctrl+Z after Save As) would be swallowed.
        (ancestors.FirstOrDefault()?.Parent as Menu)?.Close();
        Layout();
        // The tree itself is not focusable, its rows are: give the keyboard back to the selected
        // row (or the window) so the next hotkey is not swallowed by the closed popup's item.
        Control target = AnimTree.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault(row => row.IsSelected)
            ?? AnimTree.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault()
            ?? (Control)Window;
        target.Focus();
        Layout();
        if (Window.FocusManager?.GetFocusedElement() is MenuItem stuck)
        {
            throw new InvalidOperationException($"Focus is still on the menu item '{stuck.Header}' after the click; hotkeys would be swallowed.");
        }
    }

    /// <summary>
    /// Runs pending dispatcher work, lays the window out, renders a frame so hit-testing sees the
    /// new layout, and fails on an unanswered dialog.
    /// </summary>
    public void Layout()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        // Pointer input is hit-tested against the composition tree, which only updates on a
        // render tick; without this a row realized since the last tick hit-tests as its ancestor.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        Dialogs?.ThrowIfUnanswered();
    }

    /// <summary>Fails with the banner's text when the editor showed an error.</summary>
    public void ThrowIfErrorShown()
    {
        Layout();
        if (ErrorBannerText is { } error)
        {
            throw new InvalidOperationException("The editor showed an error: " + error);
        }
    }

    /// <summary>
    /// Lets real time pass with the dispatcher free to run, so <see cref="DispatcherTimer"/>s
    /// (preview playback, smooth zoom, toast auto-hide) tick. The synchronous <see cref="Wait"/>
    /// only pumps queued jobs: under the headless session timers fire only while the test yields.
    /// </summary>
    public async Task WaitAsync(TimeSpan duration)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            await Task.Delay(10);
        }
        Layout();
    }

    /// <summary>Yields to the dispatcher until <paramref name="condition"/> holds or <paramref name="timeout"/> passes; timers tick meanwhile.</summary>
    public async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(10);
        }
        Layout();
        return condition();
    }

    /// <summary>Lets real time pass while pumping queued dispatcher work; timers do not tick, see <see cref="WaitAsync"/>.</summary>
    public void Wait(TimeSpan duration)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
        Layout();
    }

    /// <summary>Pumps the dispatcher until <paramref name="condition"/> holds or <paramref name="timeout"/> passes.</summary>
    public bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            Thread.Sleep(10);
            Dispatcher.UIThread.RunJobs();
        }
        Layout();
        return condition();
    }

    public Point CenterOf(Control control)
    {
        Layout();
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException($"{control.GetType().Name} is not in the window.");
    }

    /// <summary>A point inside <paramref name="control"/>, offset from its top-left corner.</summary>
    public Point PointIn(Control control, double x, double y)
    {
        Layout();
        return control.TranslatePoint(new Point(x, y), Window)
            ?? throw new InvalidOperationException($"{control.GetType().Name} is not in the window.");
    }

    public void Click(Control control, RawInputModifiers modifiers = RawInputModifiers.None) => ClickAt(CenterOf(control), modifiers);

    public void ClickAt(Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(point, modifiers);
        Window.MouseDown(point, MouseButton.Left, modifiers);
        Window.MouseUp(point, MouseButton.Left, modifiers);
        Layout();
    }

    /// <summary>Two clicks at <paramref name="point"/> inside the double-click window.</summary>
    public void DoubleClickAt(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Layout();
    }

    public void RightClick(Control control) => RightClickAt(CenterOf(control));

    public void RightClickAt(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Right, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Right, RawInputModifiers.None);
        Layout();
    }

    public void Hover(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Layout();
    }

    /// <summary>Presses the left button at <paramref name="from"/>, moves through a few points to <paramref name="to"/>, and releases.</summary>
    public void Drag(Point from, Point to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(from, modifiers);
        Window.MouseDown(from, MouseButton.Left, modifiers);
        for (int step = 1; step <= 4; step++)
        {
            double t = step / 4.0;
            Window.MouseMove(new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t), modifiers | RawInputModifiers.LeftMouseButton);
        }
        Window.MouseUp(to, MouseButton.Left, modifiers);
        Layout();
    }

    public void Wheel(Point point, double delta, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(point, modifiers);
        Window.MouseWheel(point, new Vector(0, delta), modifiers);
        Layout();
    }

    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Layout();
    }

    /// <summary>Types <paramref name="text"/> into whatever has keyboard focus.</summary>
    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Layout();
    }

    /// <summary>Focuses <paramref name="box"/>, replaces its text by typing, and presses Enter.</summary>
    public void TypeAndEnter(TextBox box, string text)
    {
        box.Focus();
        Layout();
        box.SelectAll();
        Window.KeyTextInput(text);
        Press(Key.Enter);
    }

    /// <summary>
    /// Types a value into the inspector's <see cref="NumericUpDown"/> named <paramref name="name"/>
    /// ("PropPixelX") and commits with Enter, which also seals the coalesced undo entry so the next
    /// value typed is its own step. Focus stays in the box, as it does for a user.
    /// </summary>
    public void TypeNumber(string name, string text)
    {
        NumericUpDown input = Control<NumericUpDown>(name);
        if (!input.IsEffectivelyVisible)
        {
            throw new InvalidOperationException($"{name} is not visible; the inspector shows another kind of item.");
        }
        TextBox box = input.GetVisualDescendants().OfType<TextBox>().First();
        TypeAndEnter(box, text);
    }

    /// <summary>Types into the text box named <paramref name="name"/> ("PropTextureName") and presses Enter.</summary>
    public void TypeText(string name, string text)
    {
        TypeAndEnter(Control<TextBox>(name), text);
    }

    /// <summary>Types into the flanker numeric field named <paramref name="name"/> ("PropFrameLen", "SpeedInput") and presses Enter.</summary>
    public void TypeFlanker(string name, string text)
    {
        FlankerNumericField field = Control<FlankerNumericField>(name);
        if (!field.IsEffectivelyVisible)
        {
            throw new InvalidOperationException($"{name} is not visible; another sidebar tab or item kind is showing.");
        }
        TextBox box = field.GetVisualDescendants().OfType<TextBox>().First();
        TypeAndEnter(box, text);
    }

    #endregion

    private static IEnumerable<TreeNodeVm> Flatten(IEnumerable<TreeNodeVm> nodes)
    {
        foreach (TreeNodeVm node in nodes)
        {
            yield return node;
            foreach (TreeNodeVm child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    private static string Describe(object data) => data switch
    {
        AnimationChainSave chain => $"chain {chain.Name}",
        AnimationFrameSave frame => $"frame {frame.TextureName} at {frame.LeftCoordinate},{frame.TopCoordinate}",
        AARectSave rect => $"rectangle {rect.Name}",
        CircleSave circle => $"circle {circle.Name}",
        _ => data.GetType().Name,
    };

    public void Dispose()
    {
        try
        {
            Preview.StopPlayback();
        }
        catch
        {
            // The control may already be gone.
        }
        Window.Close();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Directory.Delete(ProjectFolder, recursive: true);
        }
        catch
        {
            // A file watcher may still hold the folder; temp is cleaned up later.
        }
    }
}
