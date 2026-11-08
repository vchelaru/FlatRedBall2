using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The Inspector tab's content on a <c>.seqj</c> tab: the sequence when nothing is selected on the timeline, a track
/// when its header is, a chain when one is. With several tracks or several chains selected the same section edits all of
/// them, as the .achx Inspector does for several frames: a field they disagree on is blank with a "(mixed)" placeholder,
/// an edit sets it on every one (or, written "+ 4", shifts each by its own value) as one undo entry, and the name and
/// Go to Definition are for one item. Every commit is one edit on the <see cref="SequenceDocument"/>, so one undo
/// entry; a refused value (an empty or taken name, cycles of 0) goes out through <see cref="ErrorRaised"/> and the field
/// goes back to the document's value. The host calls <see cref="Show"/> whenever the selection or the document changes
/// (including each step of a drag), which rewrites every field except the one being typed in.
/// </summary>
public partial class SequenceInspectorControl : UserControl
{
    private SequenceDocument? _document;
    private IUndoManager? _undo;
    private int _sequence = -1;
    private TimelineChainId? _chain;
    private int? _track;
    private IReadOnlyList<TimelineChainId> _chains = Array.Empty<TimelineChainId>();
    private IReadOnlyList<int> _tracks = Array.Empty<int>();
    private bool _canRenameSequence = true;
    private bool _refreshing;

    // The relative edit ("+ 4") being applied to a mixed field, which EditOf hands to that field's commit.
    private (Control Field, NumericEdit Edit)? _relativeEdit;

    public SequenceInspectorControl()
    {
        InitializeComponent();
        CommitOnEnterOrFocusLost(SequenceNameBox, CommitSequenceName);
        CommitOnEnterOrFocusLost(TrackNameBox, CommitTrackName);
        SequenceLoopBox.IsCheckedChanged += (_, _) => Commit(document =>
            document.SetLoop(_undo!, _sequence, SequenceLoopBox.IsChecked == true));

        TrackXField.ValueChanged += (_, _) => CommitOffset(TrackXField);
        TrackYField.ValueChanged += (_, _) => CommitOffset(TrackYField);
        TrackVisibleBox.IsCheckedChanged += (_, _) => CommitVisible();
        TrackFlipBox.IsCheckedChanged += (_, _) => CommitFlip();

        ChainStartField.ValueChanged += (_, _) => CommitChainStart();
        ChainLoopsField.ValueChanged += (_, _) => CommitChainLoops();
        // Number fields commit as they change, as in the .achx Inspector; Enter or leaving a field ends that edit's undo entry.
        SealOnCommit(TrackXField, TrackYField, ChainStartField, ChainLoopsField);
        ApplyRelativeEditsWith(() => CommitOffset(null), TrackXField, TrackYField);
        ApplyRelativeEditsWith(CommitChainStart, ChainStartField);
        ApplyRelativeEditsWith(CommitChainLoops, ChainLoopsField);
        GoToDefinitionButton.Click += (_, _) =>
        {
            if (CurrentChain is { } reference)
                GoToDefinitionRequested?.Invoke(reference);
        };
    }

    /// <summary>Raised with the reason when an edit is refused.</summary>
    public event Action<string>? ErrorRaised;

    /// <summary>Raised when the user picks Go to Definition for the selected chain.</summary>
    public event Action<SequenceChainReferenceSave>? GoToDefinitionRequested;

    private SequenceSave? CurrentSequence =>
        _document is { } document && _sequence >= 0 && _sequence < document.Save.Sequences.Count
            ? document.Save.Sequences[_sequence]
            : null;

    private SequenceTrackSave? CurrentTrack =>
        CurrentSequence is { } sequence && (_chain?.Track ?? _track) is { } index && index >= 0 && index < sequence.Tracks.Count
            ? sequence.Tracks[index]
            : null;

    private SequenceChainReferenceSave? CurrentChain =>
        _chain is { } chain && CurrentTrack is { } track && chain.Chain >= 0 && chain.Chain < track.Chains.Count
            ? track.Chains[chain.Chain]
            : null;

    /// <summary>
    /// Shows what is selected in sequence <paramref name="sequence"/> of <paramref name="document"/>: a chain, a track,
    /// or (both null) the sequence itself. <paramref name="chain"/> and <paramref name="track"/> are the primary one;
    /// <paramref name="chains"/> or <paramref name="tracks"/> hold every selected one, and with several the fields edit
    /// them all. A null document clears the inspector.
    /// </summary>
    public void Show(SequenceDocument? document, int sequence, IUndoManager? undo, TimelineChainId? chain, int? track,
        IReadOnlyList<TimelineChainId>? chains = null, IReadOnlyList<int>? tracks = null, bool canRenameSequence = true)
    {
        _chains = chains ?? Array.Empty<TimelineChainId>();
        _tracks = tracks ?? Array.Empty<int>();
        _canRenameSequence = canRenameSequence;
        _document = document;
        _sequence = sequence;
        _undo = undo;
        _chain = chain;
        _track = track;
        Refresh(force: false);
    }

    /// <summary>
    /// Writes the document's values into the fields. Unless <paramref name="force"/>, the field being typed in keeps its
    /// text, so an edit that lands meanwhile never rewrites it under the cursor.
    /// </summary>
    private void Refresh(bool force)
    {
        _refreshing = true;
        try
        {
            var sequence = CurrentSequence;
            var track = CurrentTrack;
            var reference = CurrentChain;
            NothingLabel.IsVisible = sequence is null;
            ChainPanel.IsVisible = sequence is not null && reference is not null;
            TrackPanel.IsVisible = sequence is not null && reference is null && track is not null;
            SequencePanel.IsVisible = sequence is not null && !ChainPanel.IsVisible && !TrackPanel.IsVisible;

            if (SequencePanel.IsVisible)
                ShowSequence(sequence!, force);
            else if (TrackPanel.IsVisible)
                ShowTrack(track!, force);
            else if (ChainPanel.IsVisible)
                ShowChain(reference!, force);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ShowSequence(SequenceSave sequence, bool force)
    {
        var document = _document!;
        Put(SequenceNameBox, sequence.Name, force);
        SequenceNameBox.IsEnabled = _canRenameSequence;
        SequenceLoopBox.IsChecked = sequence.Loop;
        SequenceLengthText.Text = Seconds(SequenceTimeline.Length(sequence, reference => document.CycleLength(reference) ?? 0));
        SequenceTracksText.Text = sequence.Tracks.Count.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowTrack(SequenceTrackSave track, bool force)
    {
        if (BulkTracks() is { } tracks)
        {
            // A name is one track's: the box is disabled, and says why, as for several shapes of one frame in .achx.
            var state = SequenceBulkState.ForTracks(CurrentSequence!, tracks);
            TrackNameBox.IsEnabled = false;
            TrackNameBox.Text = null;
            TrackNameBox.Tag = null;
            TrackNameBox.PlaceholderText = "(multiple — same sequence)";
            PutOrMixed(TrackXField, state.X, force);
            PutOrMixed(TrackYField, state.Y, force);
            TrackVisibleBox.IsChecked = state.Visible;
            TrackFlipBox.IsChecked = state.Flip;
            return;
        }
        TrackNameBox.IsEnabled = true;
        TrackNameBox.PlaceholderText = null;
        Put(TrackNameBox, track.Name, force);
        PutOrMixed(TrackXField, track.X, force);
        PutOrMixed(TrackYField, track.Y, force);
        TrackVisibleBox.IsChecked = track.Visible;
        TrackFlipBox.IsChecked = track.FlipHorizontal;
    }

    private void ShowChain(SequenceChainReferenceSave reference, bool force)
    {
        var document = _document!;
        if (BulkChains() is { } chains)
        {
            // Source and Chain show what the chains share, else how many there are; Length, the missing-source warning
            // and Go to Definition belong to one chain.
            var state = SequenceBulkState.ForChains(CurrentSequence!, chains);
            ChainSourceText.Text = state.Source ?? MixedPlaceholder;
            ChainNameText.Text = state.Chain ?? Plural.Format(chains.Count, "chain");
            ToolTip.SetTip(ChainSourceText, ChainSourceText.Text);
            ToolTip.SetTip(ChainNameText, ChainNameText.Text);
            PutOrMixed(ChainStartField, state.Start is { } start ? (decimal)start : null, force);
            PutOrMixed(ChainLoopsField, state.Loops is { } loops ? (decimal)loops : null, force);
            ChainLengthRow.IsVisible = false;
            ChainMissingText.IsVisible = false;
            GoToDefinitionButton.IsVisible = false;
            return;
        }
        ChainLengthRow.IsVisible = true;
        GoToDefinitionButton.IsVisible = true;
        ChainSourceText.Text = reference.Source;
        ToolTip.SetTip(ChainSourceText, reference.Source);
        ChainNameText.Text = reference.Chain;
        ToolTip.SetTip(ChainNameText, reference.Chain);
        PutOrMixed(ChainStartField, (decimal)reference.Start, force);
        PutOrMixed(ChainLoopsField, (decimal)reference.Loops, force);
        ChainLengthText.Text = document.CycleLength(reference) is { } cycle
            ? $"{Seconds(cycle * reference.Loops)} ({Seconds(cycle)} × {reference.Loops.ToString("0.##", CultureInfo.InvariantCulture)})"
            : "Unknown";
        string? problem = document.LocateDefinition(reference).Error;
        ChainMissingText.Text = problem is null ? null : "⚠ " + problem;
        ChainMissingText.IsVisible = problem is not null;
    }

    private static string Seconds(double seconds) => seconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";

    // A focused field is left alone only while it holds text the user typed and has not committed.
    private static void Put(TextBox box, string text, bool force)
    {
        bool typing = box.IsKeyboardFocusWithin && !Equals(box.Tag, box.Text);
        if (force || !typing)
        {
            box.Text = text;
            box.Tag = text;
        }
    }

    // A null value is a mixed selection: the field is blank with the "(mixed)" placeholder, as in the .achx Inspector.
    private static void PutOrMixed(FlankerNumericField field, decimal? value, bool force)
    {
        var box = field.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        string shown = field.Value?.ToString(field.FormatString, CultureInfo.InvariantCulture) ?? string.Empty;
        bool typing = field.IsKeyboardFocusWithin && box is not null && box.Text != shown;
        if (!force && typing) return;
        field.Value = value;
        field.PlaceholderText = value is null ? MixedPlaceholder : string.Empty;
    }

    private static void PutOrMixed(NumericUpDown field, decimal? value, bool force)
    {
        var box = field.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        string shown = field.Value?.ToString(field.FormatString, CultureInfo.InvariantCulture) ?? string.Empty;
        bool typing = field.IsKeyboardFocusWithin && box is not null && box.Text != shown;
        if (!force && typing) return;
        field.Value = value;
        field.PlaceholderText = value is null ? MixedPlaceholder : string.Empty;
    }

    private static void PutOrMixed(NumericUpDown field, float? value, bool force) =>
        PutOrMixed(field, value is { } v ? (decimal)v : null, force);

    private const string MixedPlaceholder = "(mixed)";

    // The selected tracks / chains when several are, else null (one item, or the selection holds nothing that exists any more).
    private IReadOnlyList<int>? BulkTracks()
    {
        if (_tracks.Count < 2 || CurrentSequence is not { } sequence) return null;
        var present = _tracks.Where(index => index >= 0 && index < sequence.Tracks.Count).ToList();
        return present.Count > 1 ? present : null;
    }

    private IReadOnlyList<TimelineChainId>? BulkChains()
    {
        if (_chains.Count < 2 || CurrentSequence is not { } sequence) return null;
        var present = _chains.Where(id => id.Track >= 0 && id.Track < sequence.Tracks.Count
            && id.Chain >= 0 && id.Chain < sequence.Tracks[id.Track].Chains.Count).ToList();
        return present.Count > 1 ? present : null;
    }

    // As MainWindow.ApplyRelativeEditsWith: "+ 4" typed over a mixed field edits each selected item's own value.
    private void ApplyRelativeEditsWith(Action apply, params Control[] fields)
    {
        foreach (var field in fields)
        {
            field.AddHandler(NumericExpressionInput.RelativeEditCommittedEvent, (_, e) =>
            {
                e.Handled = true;
                _relativeEdit = (field, e.Edit);
                try { apply(); }
                finally { _relativeEdit = null; }
                _undo?.SealCoalescing();
            });
        }
    }

    /// <summary>The edit a field asks for: the relative edit being applied to it, else its value, else null ("(mixed)" and untouched).</summary>
    private NumericEdit? EditOf(Control field, decimal? value) =>
        _relativeEdit is { } r && ReferenceEquals(r.Field, field) ? r.Edit
        : value is { } v ? v
        : null;

    // As MainWindow.SealOnCommit does for the .achx Inspector. Enter is handled by the field, so listen after it.
    private void SealOnCommit(params InputElement[] fields)
    {
        foreach (var field in fields)
        {
            field.LostFocus += (_, _) => _undo?.SealCoalescing();
            field.AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key == Key.Enter) _undo?.SealCoalescing();
            }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        }
    }

    /// <summary>
    /// Runs an edit on the document. An edit that returns a reason was refused. Either way every field is rewritten
    /// afterwards, so a refused value never stays in the box.
    /// </summary>
    private void Commit(Func<SequenceDocument, string?> edit) => RunEdit(edit);

    private void Commit(Action<SequenceDocument> edit) => RunEdit(document =>
    {
        edit(document);
        return null;
    });

    private void RunEdit(Func<SequenceDocument, string?> edit)
    {
        if (_refreshing || _document is not { } document || _undo is null) return;
        if (edit(document) is { } error)
            ErrorRaised?.Invoke(error);
        Refresh(force: true);
    }

    private void CommitOffset(Control? changed)
    {
        if (_refreshing) return;
        if (BulkTracks() is { } tracks)
        {
            // Only the component being edited applies (the other keeps each track's own value, mixed or not), so the
            // undo label names what the user changed. A relative edit names its own field.
            NumericEdit? bulkX = EditOf(TrackXField, ReferenceEquals(changed, TrackXField) ? TrackXField.Value : null);
            NumericEdit? bulkY = EditOf(TrackYField, ReferenceEquals(changed, TrackYField) ? TrackYField.Value : null);
            if (bulkX is null && bulkY is null) return;
            Commit(document => document.SetTracksOffset(_undo!, _sequence, tracks, bulkX, bulkY));
            return;
        }
        if (_track is not { } track || TrackXField.Value is not { } x || TrackYField.Value is not { } y) return;
        Commit(document => document.SetTrackOffset(_undo!, _sequence, track, (float)x, (float)y));
    }

    private void CommitVisible()
    {
        if (_refreshing) return;
        if (BulkTracks() is { } tracks)
        {
            // Indeterminate means the selection disagrees and the user did not touch it.
            if (TrackVisibleBox.IsChecked is { } visible)
                Commit(document => document.SetTracksVisible(_undo!, _sequence, tracks, visible));
            return;
        }
        Commit(document => document.SetTrackVisible(_undo!, _sequence, _track!.Value, TrackVisibleBox.IsChecked == true));
    }

    private void CommitFlip()
    {
        if (_refreshing) return;
        if (BulkTracks() is { } tracks)
        {
            if (TrackFlipBox.IsChecked is { } flip)
                Commit(document => document.SetTracksFlip(_undo!, _sequence, tracks, flip));
            return;
        }
        Commit(document => document.SetTrackFlip(_undo!, _sequence, _track!.Value, TrackFlipBox.IsChecked == true));
    }

    private void CommitChainStart()
    {
        if (_refreshing) return;
        if (BulkChains() is { } chains)
        {
            if (EditOf(ChainStartField, ChainStartField.Value) is { } edit)
                Commit(document => document.SetChainsStart(_undo!, _sequence, chains, edit));
            return;
        }
        if (ChainStartField.Value is { } start && _chain is { } chain)
            Commit(document => document.SetChainStart(_undo!, _sequence, chain.Track, chain.Chain, (double)start));
    }

    private void CommitChainLoops()
    {
        if (_refreshing) return;
        if (BulkChains() is { } chains)
        {
            if (EditOf(ChainLoopsField, ChainLoopsField.Value) is { } edit)
                Commit(document => document.SetChainsLoops(_undo!, _sequence, chains, edit));
            return;
        }
        if (ChainLoopsField.Value is { } loops && _chain is { } chain)
            Commit(document => document.SetChainLoops(_undo!, _sequence, chain.Track, chain.Chain, (float)loops));
    }

    private string? CommitSequenceName(SequenceDocument document, string text) => CurrentSequence is null
        ? null
        : document.RenameSequence(_undo!, _sequence, text);

    private string? CommitTrackName(SequenceDocument document, string text) => _track is { } track && BulkTracks() is null
        ? document.RenameTrack(_undo!, _sequence, track, text)
        : null;

    private void CommitOnEnterOrFocusLost(TextBox box, Func<SequenceDocument, string, string?> rename)
    {
        void CommitText() => Commit(document => rename(document, box.Text ?? string.Empty));
        box.LostFocus += (_, _) => CommitText();
        box.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            CommitText();
            e.Handled = true;
        };
    }
}
