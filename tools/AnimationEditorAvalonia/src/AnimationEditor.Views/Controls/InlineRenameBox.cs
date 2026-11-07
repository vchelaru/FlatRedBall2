using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// The in-place name editor of a sequence list row and a timeline track header: the same look and keys as the box
/// the ANIMATIONS tree swaps in over a chain's name. <see cref="Begin"/> seeds the text, focuses and selects it; Enter
/// or losing focus raises <see cref="CommittedEvent"/>, Escape raises <see cref="CancelledEvent"/>. Either ends the
/// edit, so the focus loss that follows a commit never commits twice. Both events bubble, so a row template can host
/// the box and the list listens once.
/// </summary>
public sealed class InlineRenameBox : TextBox
{
    public static readonly RoutedEvent<RoutedEventArgs> CommittedEvent =
        RoutedEvent.Register<InlineRenameBox, RoutedEventArgs>(nameof(Committed), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<InlineRenameBox, RoutedEventArgs>(nameof(Cancelled), RoutingStrategies.Bubble);

    private bool _editing;

    public InlineRenameBox()
    {
        BorderThickness = new Thickness(1);
        Padding = new Thickness(2, 1);
        Margin = new Thickness(0);
        MinHeight = 0;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        SelectionBrush = new SolidColorBrush(Color.Parse("#4466aa"));
        this.Bind(BackgroundProperty, this.GetResourceObservable("BgActive"));
        this.Bind(ForegroundProperty, this.GetResourceObservable("Ink"));
        this.Bind(CaretBrushProperty, this.GetResourceObservable("Ink"));
        LostFocus += (_, _) => Finish(CommittedEvent);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public event EventHandler<RoutedEventArgs>? Committed
    {
        add => AddHandler(CommittedEvent, value);
        remove => RemoveHandler(CommittedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    /// <summary>True from <see cref="Begin"/> until the edit is committed or cancelled.</summary>
    public bool IsEditing => _editing;

    /// <summary>Shows <paramref name="text"/> selected, with focus, once the box has been laid out.</summary>
    public void Begin(string text)
    {
        _editing = true;
        Text = text;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_editing) return;
            Focus();
            SelectAll();
        }, DispatcherPriority.Render);
    }

    /// <summary>Ends the edit without raising either event, for a host that is taking the box away.</summary>
    public void Abandon() => _editing = false;

    private void Finish(RoutedEvent routedEvent)
    {
        if (!_editing) return;
        _editing = false;
        RaiseEvent(new RoutedEventArgs(routedEvent));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Finish(CommittedEvent);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(CancelledEvent);
        }
        else
        {
            base.OnKeyDown(e);
            // A one-line box has no use for Up/Down; left alone they move the list's selection and end the edit.
            if (e.Key is Key.Up or Key.Down) e.Handled = true;
        }
    }
}
