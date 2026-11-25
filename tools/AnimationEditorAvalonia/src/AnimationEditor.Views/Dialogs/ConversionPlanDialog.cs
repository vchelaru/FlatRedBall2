using AnimationEditor.Core.Conversions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;
using SvgIcon = Avalonia.Svg.Skia.Svg;

namespace AnimationEditor.Views.Dialogs;

/// <summary>The Convert dialog's content: a card per kind of file in one scroll area, then the facts and the buttons, which stay put.</summary>
internal sealed class ConversionPlanDialog
{
    private const double Width = 760;
    private const double Gutter = 16;
    private const double LeftWidth = 168;
    private const double RowHeight = 24;

    /// <summary>The longest note that leaves room for the file beside it. A longer one goes below.</summary>
    private const int LongestInlineNote = 24;

    private const string Minus = "M8 12h8";
    private const string Cross = "M9 9l6 6m0-6-6 6";
    private const string Question = "M9.5 9.3a2.6 2.6 0 1 1 3.6 2.4c-.8.4-1.1.9-1.1 1.8 M12 16.8h.01";
    private const string Arrow = "M8 12h8 m-3 -3 3 3-3 3";
    private const string Check = "M20 6 9 17l-5-5";
    private const string ChevronRight = "M8 4l8 8-8 8";
    private const string ChevronDown = "M4 8l8 8 8-8";

    public static EditorDialogOptions Options =>
        new("Convert to .achj", Width, SizeToContentHeight: true) { MaxHeight = 712, EnterConfirms = false };

    private readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ProgressBar _bar = new() { Width = 120, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    private readonly TextBlock _progress = Text("", 12, "InkMid");
    private readonly DockPanel _progressRow;

    /// <summary>The dialog's content.</summary>
    public Control Root { get; }

    /// <summary>Says what is being done, in place of taking any input.</summary>
    public void ShowProgress(ConversionProgress progress)
    {
        _progress.Text = progress.Text;
        _bar.Maximum = Math.Max(1, progress.Total);
        _bar.Value = progress.Current;
        _progressRow.IsVisible = true;
        _buttons.IsEnabled = false;
    }

    /// <param name="isNewPlan">The files changed since the plan shown before.</param>
    public ConversionPlanDialog(ConversionPlanView view, bool isNewPlan, Action convert, Action cancel)
    {
        var cards = new StackPanel { Spacing = 14, Width = Width - 2 * Gutter, HorizontalAlignment = HorizontalAlignment.Left };
        // The scroll bar is in the margin, so the cards are as wide with it as without.
        var scroller = new ScrollViewer { Content = cards, AllowAutoHide = false, Margin = new Thickness(0, 0, -Gutter, 14) };
        cards.Children.Add(BuildCard(view.Animations, "IconChain", scroller));
        if (view.Sequences is { } sequences) cards.Children.Add(BuildCard(sequences, "IconSequence", scroller));

        if (view.CanConvert)
        {
            var confirm = DialogButton("Convert", convert);
            confirm.Classes.Add("primary");
            _buttons.Children.Add(confirm);
        }
        _buttons.Children.Add(DialogButton(view.CanConvert ? "Cancel" : "Close", cancel));

        // The buttons come first, so the dialog opens with Convert focused.
        var root = new DockPanel { Margin = new Thickness(Gutter) };
        Bind(_bar, ProgressBar.ForegroundProperty, "Accent");
        _progressRow = new DockPanel { IsVisible = false, Margin = new Thickness(Gutter, 0, 0, 0), Children = { _bar, _progress } };
        DockPanel.SetDock(_buttons, Dock.Right);
        var bottom = new DockPanel { Children = { _buttons, _progressRow } };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        if (isNewPlan)
        {
            var line = Text("The files are different now. This is the new plan.", 13, "AccentSoft");
            line.Margin = new Thickness(0, 0, 0, 14);
            DockPanel.SetDock(line, Dock.Top);
            root.Children.Add(line);
        }
        if (view.Facts.Count > 0)
        {
            var facts = BuildFacts(view.Facts);
            DockPanel.SetDock(facts, Dock.Bottom);
            root.Children.Add(facts);
        }
        root.Children.Add(scroller);
        root.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.PageDown) scroller.PageDown();
            else if (e.Key == Key.PageUp) scroller.PageUp();
            else return;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        var background = new Border { Child = root };
        Bind(background, Border.BackgroundProperty, "BgApp");
        Root = background;
    }

    private static Button DialogButton(string label, Action click)
    {
        var button = new Button { Content = label, MinWidth = 84, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => click();
        return button;
    }

    private static Border BuildCard(ConversionCard card, string icon, ScrollViewer scroller)
    {
        var left = new StackPanel { Margin = new Thickness(16, 14), Spacing = 2, VerticalAlignment = VerticalAlignment.Top, Children = { Heading(card.Title, icon) } };
        if (!card.IsSingle) AddCount(left, card);

        var right = new StackPanel { Margin = new Thickness(0, 10) };
        if (card.Total == 0)
            right.Children.Add(new Border { Padding = new Thickness(14, 0), MinHeight = 28, Child = Text(card.Empty, 13, "InkMid") });
        foreach (var issue in card.Issues)
            right.Children.Add(BuildIssue(issue));
        if (card.IsSingle && card.Items.Count == 1)
            right.Children.Add(BuildOnlyItem(card.Items[0]));
        else if (card.Items.Count > 0)
            right.Children.Add(BuildGroup(card));

        var divider = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
        Bind(divider, Border.BorderBrushProperty, "LineBrush");
        Grid.SetColumn(right, 1);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{LeftWidth.ToString(CultureInfo.InvariantCulture)},*"), Children = { divider, left, right } };
        var border = new Border { CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), ClipToBounds = true, Child = grid };
        Bind(border, Border.BackgroundProperty, "BgPanel");
        Bind(border, Border.BorderBrushProperty, "LineBrush");
        KeepInView(left, border, scroller);
        return border;
    }

    /// <summary>Slides <paramref name="left"/> down inside <paramref name="card"/> while the card's top is scrolled out of view.</summary>
    private static void KeepInView(Control left, Control card, ScrollViewer scroller)
    {
        var slide = new TranslateTransform();
        left.RenderTransform = slide;
        void Update() => slide.Y = Math.Clamp(scroller.Offset.Y - card.Bounds.Y, 0,
            Math.Max(0, card.Bounds.Height - left.Bounds.Height - left.Margin.Top - left.Margin.Bottom));
        scroller.ScrollChanged += (_, _) => Update();
        card.SizeChanged += (_, _) => Update();
    }

    private static StackPanel Heading(string title, string icon)
    {
        var svg = new SvgIcon(baseUri: null!)
        {
            Width = 14, Height = 14, Path = $"avares://AnimationEditor.Views/Assets/icons/svg/{icon}.svg",
            VerticalAlignment = VerticalAlignment.Center,
        };
        svg.Bind(SvgIcon.CurrentColorProperty, svg.GetResourceObservable("IconInkMuted"));
        var text = Text(title, 14, "Ink");
        text.FontWeight = FontWeight.SemiBold;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { svg, text } };
    }

    private static void AddCount(StackPanel left, ConversionCard card)
    {
        var count = Text(card.Items.Count.ToString(CultureInfo.InvariantCulture), 28, "Ink");
        count.FontWeight = FontWeight.SemiBold;
        var total = Text($"of {card.Total}", 14, "InkMid");
        total.VerticalAlignment = VerticalAlignment.Bottom;
        total.Margin = new Thickness(0, 0, 0, 4);
        left.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { count, total } });
        left.Children.Add(Text(card.Caption, 12, "InkMid"));

        var bar = new Grid { Height = 5, Margin = new Thickness(0, 10, 0, 0), ColumnSpacing = 2 };
        foreach (var (files, brush) in new[] { (card.Items.Count, "Ok"), (card.Issues.Count, "AccentSoft") })
        {
            if (files == 0) continue;
            var part = new Border { CornerRadius = new CornerRadius(2), MinWidth = 5 };
            Bind(part, Border.BackgroundProperty, brush);
            Grid.SetColumn(part, bar.ColumnDefinitions.Count);
            bar.ColumnDefinitions.Add(new ColumnDefinition(files, GridUnitType.Star) { MinWidth = 5 });
            bar.Children.Add(part);
        }
        left.Children.Add(bar);
    }

    private static Control BuildIssue(ConversionIssue issue)
    {
        string mark = issue.Mark switch { ConversionMark.StopsPlaying => Cross, ConversionMark.Unknown => Question, _ => Minus };
        var row = Row(Glyph(mark, "AccentSoft", inCircle: true), FilePath(issue.Folder, issue.Name, "Ink", 13), issue.Why);
        if (issue.Detail.Length == 0) return RowBox(row);
        var detail = Text(issue.Detail, 12, "InkMid");
        detail.TextWrapping = TextWrapping.Wrap;
        detail.Margin = new Thickness(37, 0, 33, 7);
        return Expander(row, $"{issue.Name}, {issue.Why}", detail, isOpen: false);
    }

    private static Control BuildOnlyItem(ConversionItem item)
    {
        var path = FilePath(item.Folder, item.Becomes.Length == 0 ? item.Name : $"{item.Name} → {item.Becomes}", "Ink", 13);
        var parts = new[] { item.Amount, item.Frames, item.Note }.Where(part => part.Length > 0);
        return RowBox(Row(Glyph(Arrow, "Ok", inCircle: true), path, string.Join(" · ", parts)));
    }

    private static Control BuildGroup(ConversionCard card)
    {
        var title = Text(card.GroupTitle, 13, "Ink");
        title.FontWeight = FontWeight.SemiBold;
        // Rows scroll with the cards, and only those in view exist.
        var items = new ItemsControl
        {
            ItemsSource = card.Items,
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<ConversionItem>((item, _) => item is null ? null : BuildItem(item)),
            Margin = new Thickness(0, 0, 0, 4),
        };
        var group = Expander(Row(Glyph(Arrow, "Ok", inCircle: true), title, card.GroupSummary), card.GroupTitle, items, card.ShowsItems);
        if (card.Issues.Count == 0) return group;
        var line = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(0, 6, 0, 0), Child = group };
        Bind(line, Border.BorderBrushProperty, "LineBrush");
        return line;
    }

    private static Control BuildItem(ConversionItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,78,60"), RowDefinitions = new RowDefinitions($"{RowHeight},Auto"),
            ColumnSpacing = 8, Margin = new Thickness(37, 0, 33, 0),
        };
        grid.Children.Add(FilePath(item.Folder, item.Becomes.Length == 0 ? item.Name : $"{item.Name} → {Path.GetExtension(item.Becomes)}", "Ink", 12));
        string note = item.Note;
        if (note.Length > LongestInlineNote)
        {
            var below = Text(note, 12, "InkMid");
            below.Margin = new Thickness(0, 0, 0, 5);
            Grid.SetRow(below, 1);
            Grid.SetColumnSpan(below, 4);
            grid.Children.Add(below);
            note = "";
        }
        // A sequence has one amount, in the last column.
        var amounts = item.Frames.Length == 0 ? new[] { note, "", item.Amount } : new[] { note, item.Amount, item.Frames };
        for (int i = 0; i < amounts.Length; i++)
        {
            var text = Text(amounts[i], 12, "InkMid");
            text.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(text, i + 1);
            grid.Children.Add(text);
        }
        return grid;
    }

    private static Grid Row(Control glyph, Control name, string why)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*,Auto"), ColumnSpacing = 9, MinHeight = 28 };
        var reason = Text(why, 12, "InkMid");
        Grid.SetColumn(name, 1);
        Grid.SetColumn(reason, 2);
        grid.Children.AddRange([glyph, name, reason]);
        return grid;
    }

    // The chevron's column stays, so the reasons line up.
    private static Border RowBox(Control row) => new() { Padding = new Thickness(14, 0, 33, 0), Child = row };

    private static Control Expander(Grid row, string name, Control content, bool isOpen)
    {
        var chevron = new ContentControl { Width = 10, Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Right);
        var button = new Button { Content = new DockPanel { Children = { chevron, row } }, Classes = { "expander" } };
        AutomationProperties.SetName(button, name);
        void Show(bool open)
        {
            content.IsVisible = open;
            chevron.Content = Glyph(open ? ChevronDown : ChevronRight, "InkMid", inCircle: false, size: 10, thickness: 3.5);
        }
        button.Click += (_, _) => Show(!content.IsVisible);
        Show(isOpen);
        return new StackPanel { Children = { button, content } };
    }

    /// <summary>The folder in grey, cut short when there is no room, then the name, which never is.</summary>
    private static DockPanel FilePath(string folder, string name, string nameBrush, double size)
    {
        var nameText = Text(name, size, nameBrush);
        nameText.TextTrimming = TextTrimming.CharacterEllipsis;
        var folderText = Text(folder, size, "InkMid");
        folderText.TextTrimming = TextTrimming.CharacterEllipsis;
        DockPanel.SetDock(nameText, Dock.Right);
        var path = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left, Children = { nameText, folderText } };
        ToolTip.SetTip(path, folder + name);
        return path;
    }

    private static Grid BuildFacts(IReadOnlyList<ConversionFact> facts)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("152,*"), ColumnSpacing = 15, RowSpacing = 3, Margin = new Thickness(16, 0, 16, 14) };
        foreach (var fact in facts)
        {
            int rowIndex = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = Text(fact.Label, 12, "InkMid");
            label.VerticalAlignment = VerticalAlignment.Top;
            var value = Text(fact.Value, 12, "Ink");
            value.TextWrapping = TextWrapping.Wrap;
            Control shown = value;
            if (fact.IsCheck)
            {
                var check = Glyph(Check, "Ok", inCircle: false, thickness: 3);
                check.Margin = new Thickness(0, 0, 6, 0);
                shown = new DockPanel { Children = { check, value } };
            }
            Grid.SetRow(label, rowIndex);
            Grid.SetRow(shown, rowIndex);
            Grid.SetColumn(shown, 1);
            grid.Children.AddRange([label, shown]);
        }
        return grid;
    }

    private static TextBlock Text(string text, double size, string brush)
    {
        var block = new TextBlock { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        Bind(block, TextBlock.ForegroundProperty, brush);
        return block;
    }

    private static Viewbox Glyph(string data, string brush, bool inCircle, double size = 14, double thickness = 2)
    {
        if (inCircle) data = $"M21 12a9 9 0 1 1-18 0a9 9 0 1 1 18 0 {data}";
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        Bind(path, Avalonia.Controls.Shapes.Shape.StrokeProperty, brush);
        return new Viewbox { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
    }

    private static void Bind(Control control, AvaloniaProperty property, string token) =>
        control.Bind(property, control.GetResourceObservable(token));
}
