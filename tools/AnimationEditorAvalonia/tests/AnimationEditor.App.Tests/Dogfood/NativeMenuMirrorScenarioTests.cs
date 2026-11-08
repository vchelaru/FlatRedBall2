using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Audit 5 of the macOS system menu mirror (#1681, #1689): whether the parity check itself notices each kind of drift,
/// whether the bar follows the in-window menu through every later change (headers, gestures, toggle state, children
/// arriving and leaving, other tab kinds), and whether it lets go of what it dropped.
/// </summary>
public class NativeMenuMirrorScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenHeroAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        return editor;
    }

    private static NativeMenuItem Find(NativeMenu bar, params string[] path) => NativeMenuTestSupport.Find(bar, path);

    /// <summary>The in-window item at <paramref name="path"/> (headers without access-key underscores).</summary>
    private static MenuItem InWindow(AnimationEditorHarness editor, params string[] path)
    {
        ItemsControl level = editor.Control<Menu>("MainMenu");
        MenuItem? item = null;
        foreach (string header in path)
        {
            item = level.Items.OfType<MenuItem>().Single(candidate => Regex.Replace((string?)candidate.Header ?? "", "_(.)", "$1") == header);
            level = item;
        }
        return item!;
    }

    private static List<string> Differences(AnimationEditorHarness editor, NativeMenu bar) =>
        NativeMenuParity.Differences(editor.Control<Menu>("MainMenu"), bar, CommandModifier.Meta);

    private static NativeMenu BuildInSync(AnimationEditorHarness editor)
    {
        NativeMenu bar = NativeMenuTestSupport.Build(editor);
        Differences(editor, bar).ShouldBeEmpty();
        return bar;
    }

    private sealed class FixedCommand(bool canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) { }
    }

    // ---- the parity check notices each kind of drift ----

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAWrongHeader()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "Edit", "Undo").Header = "Undone";

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Undo") && difference.Contains("header"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAWrongEnabledState()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenuItem save = Find(bar, "File", "Save");
        save.IsEnabled = !save.IsEnabled;

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Save") && difference.Contains("enabled"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAWrongCheckedState()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenuItem hotReload = Find(bar, "Edit", "Enable Hot Reload");
        hotReload.IsChecked = !hotReload.IsChecked;

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Hot Reload") && difference.Contains("checked"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAWrongToggleType()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "Edit", "Enable Hot Reload").ToggleType = MenuItemToggleType.None;

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Hot Reload") && difference.Contains("toggle type"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAMissingSubmenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "File", "Export").Menu = null;

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Export") && difference.Contains("no submenu"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAnItemMissingFromTheEndOfAMenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenu edit = Find(bar, "Edit").Menu!;
        edit.Items.RemoveAt(edit.Items.Count - 1);

        Differences(editor, bar).ShouldContain(difference => difference.Contains("missing from the native bar"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsAnExtraNativeItem_InASubmenuAndOnTheBar()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "Edit").Menu!.Add(new NativeMenuItem("Stray"));
        Differences(editor, bar).ShouldContain(difference => difference.Contains("extra"));

        NativeMenu second = BuildInSync(editor);
        second.Add(new NativeMenuItem("Stray"));
        Differences(editor, second).ShouldContain(difference => difference.Contains("extra"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsASeparatorWhereAnItemBelongs_AndAnItemWhereASeparatorBelongs()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenu edit = Find(bar, "Edit").Menu!;
        edit.Items[0] = new NativeMenuItemSeparator();
        Differences(editor, bar).ShouldContain(difference => difference.Contains("native item is a separator"));

        NativeMenu second = BuildInSync(editor);
        NativeMenu secondEdit = Find(second, "Edit").Menu!;
        int separator = secondEdit.Items.ToList().FindIndex(item => item is NativeMenuItemSeparator);
        secondEdit.Items[separator] = new NativeMenuItem("Not a separator");
        Differences(editor, second).ShouldContain(difference => difference.Contains("not a separator"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsDriftInANestedItem_WithItsFullPath()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "File", "Export", "PixiJS…").Header = "Pixie";

        Differences(editor, bar).ShouldContain(difference => difference.Contains("/File/Export/PixiJS…") && difference.Contains("header"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsTwoItemsInTheWrongOrder()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenu edit = Find(bar, "Edit").Menu!;
        NativeMenuItemBase undo = edit.Items[0];
        edit.Items.RemoveAt(0);
        edit.Items.Insert(1, undo);

        Differences(editor, bar).ShouldNotBeEmpty();
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsALeafWithoutACommand_ASubmenuWithOne_AndALeafWithASubmenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        Find(bar, "Edit", "Undo").Command = null;
        Differences(editor, bar).ShouldContain(difference => difference.Contains("Undo") && difference.Contains("no command"));

        NativeMenu second = BuildInSync(editor);
        Find(second, "File", "Export").Command = new FixedCommand(true);
        Differences(editor, second).ShouldContain(difference => difference.Contains("Export") && difference.Contains("also has a command"));

        NativeMenu third = BuildInSync(editor);
        Find(third, "Edit", "Undo").Menu = new NativeMenu();
        Differences(editor, third).ShouldContain(difference => difference.Contains("Undo") && difference.Contains("has a submenu"));
    }

    [AvaloniaFact]
    public async Task ParityCheck_ReportsACommandThatCanExecuteWhileTheInWindowItemIsDisabled()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem undo = InWindow(editor, "Edit", "Undo");
        undo.IsEnabled.ShouldBeFalse();
        Find(bar, "Edit", "Undo").Command = new FixedCommand(true);

        Differences(editor, bar).ShouldContain(difference => difference.Contains("Undo") && difference.Contains("can execute"));
    }

    // ---- the bar follows the in-window menu ----

    [AvaloniaFact]
    public async Task TheBar_StaysInParity_AsTheActiveTabChangesKind()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);

        string sheet = editor.WritePng("pane.png", 32, 32);
        await editor.OpenPngTabAsync(sheet);
        Differences(editor, bar).ShouldBeEmpty();
        InWindow(editor, "File", "Save As…").IsEnabled.ShouldBeFalse();

        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        Differences(editor, bar).ShouldBeEmpty();

        editor.ClickTab("hero.achx");
        Differences(editor, bar).ShouldBeEmpty();
        InWindow(editor, "File", "Save As…").IsEnabled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task TheBar_OfAWindowWithNoTab_IsInParity()
    {
        using var editor = new AnimationEditorHarness();
        BuildInSync(editor);
    }

    [AvaloniaFact]
    public async Task AddingAndRemovingInWindowItems_LeavesNoStaleOrDuplicateNativeItems()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem edit = InWindow(editor, "Edit");

        edit.Items.Add(new Separator());
        var added = new MenuItem { Header = "_Brand New" };
        edit.Items.Add(added);
        edit.Items.Insert(0, new MenuItem { Header = "_First" });
        Differences(editor, bar).ShouldBeEmpty();
        Find(bar, "Edit").Menu!.Items.Count.ShouldBe(edit.Items.Count);

        edit.Items.Remove(added);
        edit.Items.RemoveAt(0);
        Differences(editor, bar).ShouldBeEmpty();
        Find(bar, "Edit").Menu!.Items.Count.ShouldBe(edit.Items.Count);
    }

    [AvaloniaFact]
    public async Task AHeaderChange_IsFollowed_WithItsAccessKeyUnderscoreDropped()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);

        InWindow(editor, "Edit", "Undo").Header = "_Take Back";

        Find(bar, "Edit", "Take Back").ShouldNotBeNull();
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AGestureChange_IsFollowed_OnTheMacCommandKey()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);

        InWindow(editor, "Edit", "Undo").InputGesture = new KeyGesture(Key.U, KeyModifiers.Control);

        Find(bar, "Edit", "Undo").Gesture.ShouldBe(new KeyGesture(Key.U, KeyModifiers.Meta));
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AToggleTypeChange_IsFollowed()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);

        InWindow(editor, "Edit", "Copy").ToggleType = MenuItemToggleType.CheckBox;

        Find(bar, "Edit", "Copy").ToggleType.ShouldBe(MenuItemToggleType.CheckBox);
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheDiagnosticsCheckmark_FollowsTheF3Hotkey()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        NativeMenuItem diagnostics = Find(bar, "Help", "Show Render Diagnostics");
        diagnostics.IsChecked.ShouldBeFalse();

        editor.Press(Key.F3);

        InWindow(editor, "Help", "Show Render Diagnostics").IsChecked.ShouldBeTrue();
        diagnostics.IsChecked.ShouldBeTrue();
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ADisabledSubmenuItem_IsGreyedInTheBar()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem export = InWindow(editor, "File", "Export");

        export.IsEnabled = false;
        Find(bar, "File", "Export").IsEnabled.ShouldBeFalse();

        export.IsEnabled = true;
        Find(bar, "File", "Export").IsEnabled.ShouldBeTrue();
    }

    // ---- items with children come and go ----

    [AvaloniaFact]
    public async Task OpenRecent_ShowsAFileNameWithAnUnderscoreVerbatim()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("my_walk.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        NativeMenu bar = NativeMenuTestSupport.Build(editor);

        await editor.OpenAsync(path);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        Find(bar, "File", "Open Recent").Menu!.Items.OfType<NativeMenuItem>().Select(item => item.Header)
            .ShouldContain("my_walk.achx");
    }

    [AvaloniaFact]
    public async Task OpenRecent_WhenItEmptiesAndFillsAgain_SwitchesBetweenAnActionAndASubmenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem recent = InWindow(editor, "File", "Open Recent");
        NativeMenuItem native = Find(bar, "File", "Open Recent");
        recent.Items.Count.ShouldBeGreaterThan(0);
        native.Menu.ShouldNotBeNull();
        native.Command.ShouldBeNull();
        NativeMenu emptied = native.Menu!;

        recent.Items.Clear();

        native.Menu.ShouldBeNull();
        native.Command.ShouldNotBeNull();
        Differences(editor, bar).ShouldBeEmpty();
        int opened = 0, clicks = 0;
        recent.AddHandler(MenuItem.SubmenuOpenedEvent, (_, _) => opened++);
        recent.Click += (_, _) => clicks++;
        NativeMenuTestSupport.RaiseOpening(emptied);
        opened.ShouldBe(0);   // the dropped submenu no longer forwards its opening
        native.Command!.Execute(null);
        clicks.ShouldBe(1);

        recent.Items.Add(new MenuItem { Header = "again.achx" });

        native.Menu.ShouldNotBeNull();
        native.Menu!.Items.OfType<NativeMenuItem>().Select(item => item.Header).ShouldBe(new[] { "again.achx" });
        native.Command.ShouldBeNull();
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ASubmenuLeftWithOneChild_IsStillASubmenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem export = InWindow(editor, "File", "Export");

        while (export.Items.Count > 1) export.Items.RemoveAt(export.Items.Count - 1);

        Find(bar, "File", "Export").Menu.ShouldNotBeNull();
        Find(bar, "File", "Export").Menu!.Items.Count.ShouldBe(1);
        Differences(editor, bar).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AddingAChildToASubmenu_KeepsTheSameNativeSubmenu_AndItStillForwardsOpeningOnce()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem export = InWindow(editor, "File", "Export");
        NativeMenu menu = Find(bar, "File", "Export").Menu!;

        export.Items.Add(new MenuItem { Header = "_Extra Format…" });

        Find(bar, "File", "Export").Menu.ShouldBeSameAs(menu);
        menu.Items.Count.ShouldBe(export.Items.Count);
        int opened = 0;
        export.AddHandler(MenuItem.SubmenuOpenedEvent, (_, e) => { if (ReferenceEquals(e.Source, export)) opened++; });
        NativeMenuTestSupport.RaiseOpening(menu);
        opened.ShouldBe(1);
        Differences(editor, bar).ShouldBeEmpty();
    }

    // ---- activation ----

    [AvaloniaFact]
    public async Task ActivatingAnItemWithoutACheckmark_LeavesItsCheckedStateAlone()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem reload = InWindow(editor, "Edit", "Reload from Disk");
        NativeMenuItem native = Find(bar, "Edit", "Reload from Disk");

        native.Command!.Execute(null);

        reload.IsChecked.ShouldBeFalse();
        native.IsChecked.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task AfterActivating_TheNativeCheckmarkMatchesTheInWindowOne_EvenIfTheSystemFlippedItMeanwhile()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        MenuItem inWindow = InWindow(editor, "Edit", "Enable Hot Reload");
        NativeMenuItem native = Find(bar, "Edit", "Enable Hot Reload");
        inWindow.Click += (_, _) => native.IsChecked = !inWindow.IsChecked;   // AppKit flips its own checkmark as the action runs

        native.Command!.Execute(null);

        native.IsChecked.ShouldBe(inWindow.IsChecked);
    }

    [AvaloniaFact]
    public async Task OpeningAMenu_RaisesSubmenuOpenedOnItsOwnInWindowItemOnce_AndNeverClicksIt()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        foreach (string[] path in new[] { new[] { "File" }, new[] { "Edit" }, new[] { "File", "Export" } })
        {
            MenuItem item = InWindow(editor, path);
            int opened = 0, clicks = 0;
            item.AddHandler(MenuItem.SubmenuOpenedEvent, (_, e) => { if (ReferenceEquals(e.Source, item)) opened++; });
            item.Click += (_, _) => clicks++;

            NativeMenuTestSupport.RaiseOpening(Find(bar, path).Menu!);

            opened.ShouldBe(1, string.Join(" > ", path));
            clicks.ShouldBe(0, string.Join(" > ", path));
        }
    }

    // ---- letting go of what was dropped ----

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WeakFind(NativeMenu bar, params string[] path) => new(Find(bar, path));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WeakSubmenuOf(NativeMenu bar, params string[] path) => new(Find(bar, path).Menu);

    private static void CollectGarbage()
    {
        for (int pass = 0; pass < 3; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [AvaloniaFact]
    public async Task RebuildingAMenu_ReleasesTheNativeItemsItDropped()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        WeakReference dropped = WeakFind(bar, "Edit", "Undo");

        InWindow(editor, "Edit").Items.Add(new MenuItem { Header = "_Brand New" });
        CollectGarbage();

        dropped.IsAlive.ShouldBeFalse();
        GC.KeepAlive(bar);
    }

    [AvaloniaFact]
    public async Task RebuildingAMenu_ReleasesTheNestedItemsOfTheSubmenusItDropped()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        WeakReference dropped = WeakFind(bar, "File", "Export", "PixiJS…");

        InWindow(editor, "File").Items.Add(new MenuItem { Header = "_Brand New" });
        CollectGarbage();

        dropped.IsAlive.ShouldBeFalse();
        GC.KeepAlive(bar);
    }

    [AvaloniaFact]
    public async Task ASubmenuThatEmpties_ReleasesTheItemsItDropped()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildInSync(editor);
        WeakReference dropped = WeakFind(bar, "File", "Export", "PixiJS…");
        WeakReference droppedMenu = WeakSubmenuOf(bar, "File", "Export");

        InWindow(editor, "File", "Export").Items.Clear();
        CollectGarbage();

        dropped.IsAlive.ShouldBeFalse();
        droppedMenu.IsAlive.ShouldBeFalse();
        GC.KeepAlive(bar);
    }

    // ---- pure mapping ----

    [Fact]
    public void ToMacGesture_MovesCtrlToCmd_AndKeepsTheKeyAndEveryOtherModifier()
    {
        NativeMenuBuilder.ToMacGesture(null).ShouldBeNull();
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.S, KeyModifiers.Control)).ShouldBe(new KeyGesture(Key.S, KeyModifiers.Meta));
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift))
            .ShouldBe(new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift));
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.Z, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift))
            .ShouldBe(new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift));
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.Left, KeyModifiers.Alt)).ShouldBe(new KeyGesture(Key.Left, KeyModifiers.Alt));
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.F3)).ShouldBe(new KeyGesture(Key.F3));
        NativeMenuBuilder.ToMacGesture(new KeyGesture(Key.N, KeyModifiers.Meta)).ShouldBe(new KeyGesture(Key.N, KeyModifiers.Meta));
    }
}
