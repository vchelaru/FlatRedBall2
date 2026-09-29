using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using FlatRedBall2.Animation;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Inspector section listing a frame's <see cref="AnimationFrameEvent"/>s as editable Name/Data
/// rows. It never mutates events itself: it raises <see cref="AddRequested"/>,
/// <see cref="EditCommitted"/>, and <see cref="RemoveRequested"/> for the host to route through
/// undoable commands, then the host calls <see cref="ShowEvents"/> again with the result.
/// </summary>
public partial class FrameEventsEditor : UserControl
{
    private sealed record Row(TextBox Name, TextBox Data, Button Remove);

    private readonly List<Row> _rows = new();
    private IReadOnlyList<AnimationFrameEvent> _shown = Array.Empty<AnimationFrameEvent>();
    private bool _focusNewRow;

    /// <summary>The user clicked Add.</summary>
    public event Action? AddRequested;

    /// <summary>A row's Name or Data was edited and committed: (index, name, data). Blank names never commit.</summary>
    public event Action<int, string, string?>? EditCommitted;

    /// <summary>The user clicked a row's remove button: the event's index.</summary>
    public event Action<int>? RemoveRequested;

    public FrameEventsEditor()
    {
        InitializeComponent();
        AddButton.Click += (_, _) =>
        {
            _focusNewRow = true;
            AddRequested?.Invoke();
        };
    }

    /// <summary>Name/Data text boxes, one pair per shown event, in order.</summary>
    public IReadOnlyList<(TextBox Name, TextBox Data, Button Remove)> Rows =>
        _rows.ConvertAll(r => (r.Name, r.Data, r.Remove));

    /// <summary>
    /// Shows <paramref name="events"/> for editing, or, when <c>null</c> (several frames selected),
    /// hides the rows and the add button behind a hint. Rows are reused when the count is unchanged,
    /// so a commit that triggers a refresh doesn't steal focus from the box the user tabbed into.
    /// </summary>
    public void ShowEvents(IReadOnlyList<AnimationFrameEvent>? events)
    {
        AddButton.IsVisible = events is not null;
        RowsPanel.IsVisible = events is not null;
        HintText.IsVisible = events is null || events.Count == 0;
        HintText.Text = events is null ? "Select a single frame to edit its events." : "No events.";
        _shown = events ?? Array.Empty<AnimationFrameEvent>();

        if (_rows.Count != _shown.Count)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();
            for (int i = 0; i < _shown.Count; i++)
                AddRow(i);
        }
        for (int i = 0; i < _shown.Count; i++)
        {
            SetTextIfChanged(_rows[i].Name, _shown[i].Name);
            SetTextIfChanged(_rows[i].Data, _shown[i].Data ?? string.Empty);
        }

        if (_focusNewRow && _rows.Count > 0)
        {
            var nameBox = _rows[^1].Name;
            nameBox.Focus();
            nameBox.SelectAll();
        }
        _focusNewRow = false;
    }

    private void AddRow(int index)
    {
        var name = new TextBox { PlaceholderText = "Name", FontSize = 11, MinWidth = 60 };
        var data = new TextBox { PlaceholderText = "Data (optional)", FontSize = 11, MinWidth = 60, Margin = new(4, 0, 0, 0) };
        var remove = new Button
        {
            Content = "×", FontSize = 12, Width = 22, Height = 22, Padding = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0, 0, 0),
        };
        ToolTip.SetTip(remove, "Remove this event");
        Grid.SetColumn(data, 1);
        Grid.SetColumn(remove, 2);
        RowsPanel.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,Auto"),
            Children = { name, data, remove },
        });

        foreach (var box in new[] { name, data })
        {
            box.LostFocus += (_, _) => Commit(index);
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(index); };
        }
        remove.Click += (_, _) => RemoveRequested?.Invoke(index);
        _rows.Add(new Row(name, data, remove));
    }

    private void Commit(int index)
    {
        if (index >= _shown.Count) return;
        var row = _rows[index];
        var shown = _shown[index];
        var name = row.Name.Text?.Trim() ?? string.Empty;
        var data = string.IsNullOrWhiteSpace(row.Data.Text) ? null : row.Data.Text;
        if (name.Length == 0)
        {
            // An unnamed event can't be matched by game code; restore instead of committing.
            row.Name.Text = shown.Name;
            return;
        }
        if (name == shown.Name && data == shown.Data) return;
        EditCommitted?.Invoke(index, name, data);
    }

    private static void SetTextIfChanged(TextBox box, string text)
    {
        if (box.Text != text) box.Text = text;
    }
}
