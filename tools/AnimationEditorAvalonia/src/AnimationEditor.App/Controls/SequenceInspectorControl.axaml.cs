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
/// entry; a refused value (an empty or taken name, a length of 0) goes out through <see cref="ErrorRaised"/> and the field
/// goes back to the document's value. The host calls <see cref="Show"/> whenever the selection or the document changes
/// (including each step of a drag), which rewrites every field except the one being typed in. Showing another item (one
/// put in its place by a change outside the editor included) drops the typed text: it only ever applies to what it was typed for.
/// </summary>
public partial class SequenceInspectorControl : UserControl
{
    private SequenceDocument? _document;
    private IUndoManager? _undo;
    private int _sequence = -1;
    private TimelineChainId? _chain;
    private int? _track;
    private string? _goToSequence; // the sequence the shown reference track plays
    private IReadOnlyList<TimelineChainId> _chains = Array.Empty<TimelineChainId>();
    private IReadOnlyList<int> _tracks = Array.Empty<int>();
    private bool _canRenameSequence = true;
    private bool _refreshing;
    private List<object> _shown = new();

    // The relative edit ("+ 4") being applied to a mixed field, which EditOf hands to that field's commit.
    private (Control Field, NumericEdit Edit)? _relativeEdit;

    public SequenceInspectorControl()
    {
        InitializeComponent();
        CommitOnEnterOrFocusLost(SequenceNameBox, CommitSequenceName);
        CommitOnEnterOrFocusLost(TrackNameBox, CommitTrackName);
        SequenceLoopBox.IsCheckedChanged += (_, _) => Commit(document =>
            document.SetLoop(_undo!, _sequence, SequenceLoopBox.IsChecked == true));

        TrackVisibleBox.IsCheckedChanged += (_, _) => CommitVisible();
        TrackContinuousBox.IsCheckedChanged += (_, _) => CommitContinuous();

        ChainStartField.ValueChanged += (_, _) => CommitChainStart();
        ChainLengthField.ValueChanged += (_, _) => CommitChainLength();
        // Number fields commit as they change, as in the .achx Inspector; Enter or leaving a field ends that edit's undo entry.
        SealOnCommit(ChainStartField, ChainLengthField, ReferenceStartField, ReferenceLengthField);
        ReferenceStartField.ValueChanged += (_, _) => CommitReference(start: true);
        ReferenceLengthField.ValueChanged += (_, _) => CommitReference(start: false);
        ApplyRelativeEditsWith(CommitChainStart, ChainStartField);
        ApplyRelativeEditsWith(CommitChainLength, ChainLengthField);
        // An unset length follows the chain (or the clip's target); clearing the field goes back to that, as a blank
        // .achx color channel goes back to inherited.
        ChainLengthField.IsClearable = true;
        ReferenceLengthField.IsClearable = true;
        ChainLengthField.Cleared += (_, _) => ClearChainLength();
        ReferenceLengthField.Cleared += (_, _) =>
        {
            if (!_refreshing && _track is { } track)
                Commit(document => document.ClearReferenceLength(_undo!, _sequence, track));
        };
        GoToDefinitionButton.Click += (_, _) =>
        {
            if (CurrentChain is { } reference)
                GoToDefinitionRequested?.Invoke(reference);
        };
        ChainNameBox.SelectionChanged += (_, _) => RelinkToSelectedChain();
        GoToSequenceButton.Click += (_, _) =>
        {
            if (_goToSequence is { } sequenceName)
                GoToSequenceRequested?.Invoke(sequenceName);
        };
    }

    /// <summary>Raised with the sequence's name when the user presses Go to Sequence on a reference track.</summary>
    public event Action<string>? GoToSequenceRequested;

    // The dropdown's entries as chain names, parallel to its items; null for the "(missing)" entry that stands for the
    // current name when the source lacks it, which is not a choice.
    private List<string?> _chainChoices = new();

    private void RelinkToSelectedChain()
    {
        if (_refreshing) return;
        int index = ChainNameBox.SelectedIndex;
        if (index < 0 || index >= _chainChoices.Count || _chainChoices[index] is not { } name) return;
        if (BulkChains() is { } chains)
            Commit(document => document.RelinkChains(_undo!, _sequence, chains, name));
        else if (_chain is { } chain)
            Commit(document => document.RelinkChains(_undo!, _sequence, new[] { chain }, name));
    }

    // The Chain dropdown: the chains of the source (of every selected chain, when they share one), with the current name
    // selected. A name the source lacks, or several chains with different names, shows as a first entry that is no choice.
    private void ShowChainChoices(string? source, string? chain, string mixedLabel)
    {
        var available = source is null ? new List<string>()
            : _document!.SourceChains(source)?.AnimationChains.Select(c => c.Name).ToList() ?? new List<string>();
        int current = chain is null ? -1 : available.IndexOf(chain);
        var choices = available.Cast<string?>().ToList();
        var shown = new List<string>(available);
        if (source is null)
        {
            // Chains of different sources have no chain list in common, so there is nothing to pick.
            choices = new List<string?> { null };
            shown = new List<string> { chain ?? mixedLabel };
            current = 0;
        }
        else if (current < 0)
        {
            choices.Insert(0, null);
            shown.Insert(0, chain is null ? mixedLabel : chain.Length == 0 ? "(missing)" : $"{chain} (missing)");
            current = 0;
        }
        if (!shown.SequenceEqual(ChainNameBox.Items.OfType<string>()))
            ChainNameBox.ItemsSource = shown;
        _chainChoices = choices;
        ChainNameBox.SelectedIndex = current;
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
        bool sameDocument = ReferenceEquals(document, _document) || document?.Path == _document?.Path;
        _chains = chains ?? Array.Empty<TimelineChainId>();
        _tracks = tracks ?? Array.Empty<int>();
        _canRenameSequence = canRenameSequence;
        _document = document;
        _sequence = sequence;
        _undo = undo;
        _chain = chain;
        _track = track;
        var shown = ShownItems();
        if (!sameDocument || !shown.SequenceEqual(_shown)) DiscardTypedText();
        _shown = shown;
        Refresh(force: false);
    }

    // What the fields edit, told apart by content: after a change outside the editor an index can hold another item.
    private List<object> ShownItems()
    {
        var items = new List<object>();
        if (CurrentSequence is not { } sequence) return items;
        items.Add(sequence.Name);
        foreach (int index in _tracks.Append(_track ?? -1).Distinct().Where(index => index >= 0 && index < sequence.Tracks.Count))
        {
            var track = sequence.Tracks[index];
            items.Add((index, track.Name));
            if (SequenceTimeline.OrderedClips(track).FirstOrDefault() is { } clip)
                items.Add((clip.Sequence, clip.Start, clip.Length));
        }
        foreach (var id in (_chain is { } chain ? _chains.Append(chain) : _chains).Distinct())
        {
            if (id.Track >= sequence.Tracks.Count || id.Chain >= sequence.Tracks[id.Track].Chains.Count) continue;
            var block = sequence.Tracks[id.Track].Chains[id.Chain];
            items.Add((id, sequence.Tracks[id.Track].Name, block.Source, block.Chain, block.Start, block.Length));
        }
        return items;
    }

    private void DiscardTypedText()
    {
        _refreshing = true;
        try
        {
            SequenceNameBox.Text = SequenceNameBox.Tag as string;
            TrackNameBox.Text = TrackNameBox.Tag as string;
            foreach (var field in new[] { ChainStartField, ChainLengthField, ReferenceStartField, ReferenceLengthField })
                field.DiscardTypedText();
        }
        finally
        {
            _refreshing = false;
        }
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

            // Also while its section is hidden: a box losing the focus late still commits its text to the sequence shown.
            if (sequence is not null) Put(SequenceNameBox, sequence.Name, force);
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
        SequenceNameBox.IsEnabled = _canRenameSequence;
        SequenceLoopBox.IsChecked = sequence.Loop;
        SequenceLengthText.Text = Seconds(SequenceTimeline.Length(document.Save.Sequences, sequence, reference => document.CycleLength(reference) ?? 0));
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
            TrackVisibleBox.IsChecked = state.Visible;
            TrackContinuousBox.IsChecked = state.Continuous;
            ReferenceSection.IsVisible = false;
            return;
        }
        TrackNameBox.IsEnabled = true;
        TrackNameBox.PlaceholderText = null;
        Put(TrackNameBox, track.Name, force);
        TrackVisibleBox.IsChecked = track.Visible;
        TrackContinuousBox.IsChecked = track.Continuous;
        ShowReference(track, force);
    }

    // A track that plays another sequence shows its clip: which sequence, where it starts and how long it lasts.
    private void ShowReference(SequenceTrackSave track, bool force)
    {
        ReferenceSection.IsVisible = track.References is not null;
        // The Inspector shows the track's first clip (by start); the other clips are edited on the timeline.
        if (SequenceTimeline.OrderedClips(track).FirstOrDefault() is not { } reference) { ReferenceSection.IsVisible = false; return; }
        ReferenceSequenceText.Text = reference.Sequence;
        PutOrMixed(ReferenceStartField, (decimal)reference.Start, force);
        PutOrMixed(ReferenceLengthField, (decimal)_document!.ClipLength(reference), force);
        var sequences = _document!.Save.Sequences;
        bool missing = SequenceReferences.Find(sequences, reference.Sequence) is null;
        var cycle = !missing && _sequence >= 0 && _sequence < sequences.Count
            ? SequenceReferences.CycleThrough(sequences, sequences[_sequence].Name, reference.Sequence) : null;
        ReferenceMissingText.Text = missing ? $"⚠ There is no sequence named \"{reference.Sequence}\"."
            : cycle is not null ? $"⚠ {SequenceCycleNotice.Describe(cycle)}." : null;
        ReferenceMissingText.IsVisible = ReferenceMissingText.Text is not null;
        _goToSequence = reference.Sequence;
        GoToSequenceButton.IsEnabled = !missing;
        ToolTip.SetTip(GoToSequenceButton, TimelineMenuLayout.GoToSequenceDisabledReason(_document.Save.Sequences, reference.Sequence)
            ?? "Select this sequence in the list");
    }

    private void CommitReference(bool start)
    {
        if (_refreshing || _track is not { } track) return;
        if (start && ReferenceStartField.Value is { } at)
            Commit(document => document.SetReferenceStart(_undo!, _sequence, track, (double)at, FirstClipIndex(document, track)));
        else if (!start && ReferenceLengthField.Value is { } length)
            Commit(document => document.SetReferenceLength(_undo!, _sequence, track, (double)length, FirstClipIndex(document, track)));
    }

    // The list index of the clip the Inspector shows: the track's earliest.
    private int FirstClipIndex(SequenceDocument document, int track)
    {
        var clips = document.Save.Sequences[_sequence].Tracks[track].References!;
        return clips.IndexOf(SequenceTimeline.OrderedClips(document.Save.Sequences[_sequence].Tracks[track])[0]);
    }

    private void ShowChain(SequenceChainReferenceSave reference, bool force)
    {
        var document = _document!;
        if (BulkChains() is { } chains)
        {
            // Source and Chain show what the chains share, else how many there are; Length, the missing-source warning
            // and Go to Definition belong to one chain.
            var state = SequenceBulkState.ForChains(CurrentSequence!, chains, document.ChainLength);
            ChainSourceText.Text = state.Source ?? MixedPlaceholder;
            ShowChainChoices(state.Source, state.Chain, Plural.Format(chains.Count, "chain"));
            ToolTip.SetTip(ChainSourceText, ChainSourceText.Text);
            PutOrMixed(ChainStartField, state.Start is { } start ? (decimal)start : null, force);
            PutOrMixed(ChainLengthField, state.Length is { } length ? (decimal)length : null, force);
            ChainCycleRow.IsVisible = false;
            ChainMissingText.IsVisible = false;
            GoToDefinitionButton.IsVisible = false;
            return;
        }
        ChainCycleRow.IsVisible = true;
        GoToDefinitionButton.IsVisible = true;
        ChainSourceText.Text = reference.Source;
        ToolTip.SetTip(ChainSourceText, reference.Source);
        ShowChainChoices(reference.Source, reference.Chain, string.Empty);
        PutOrMixed(ChainStartField, (decimal)reference.Start, force);
        PutOrMixed(ChainLengthField, (decimal)document.ChainLength(reference), force);
        ChainCycleText.Text = ChainCycleLabel(reference.Length, document.CycleLength(reference));
        string? problem = document.LocateDefinition(reference).Error;
        ChainMissingText.Text = problem is null ? null : "⚠ " + problem;
        ChainMissingText.IsVisible = problem is not null;
    }

    private static string Seconds(double seconds) => BlockLabel.Seconds(seconds);

    /// <summary>
    /// The Cycle row: the chain's cycle, then how many cycles a set length spans, or that an unset length follows the
    /// cycle. "Unknown" for a missing chain.
    /// </summary>
    internal static string ChainCycleLabel(double? length, double? cycle) => cycle is not { } c || c <= 0 ? "Unknown"
        : length is { } l ? $"{Seconds(c)} (×{(l / c).ToString("0.##", CultureInfo.InvariantCulture)})"
        : $"{Seconds(c)} (length follows it)";

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

    private void CommitContinuous()
    {
        if (_refreshing) return;
        // Indeterminate means a multi-selection that disagrees and the user did not touch it.
        if (TrackContinuousBox.IsChecked is not { } continuous) return;
        var tracks = BulkTracks() ?? new[] { _track!.Value };
        Commit(document => document.SetTracksContinuous(_undo!, _sequence, tracks, continuous));
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

    private void CommitChainLength()
    {
        if (_refreshing) return;
        if (BulkChains() is { } chains)
        {
            if (EditOf(ChainLengthField, ChainLengthField.Value) is { } edit)
                Commit(document => document.SetChainsLength(_undo!, _sequence, chains, edit));
            return;
        }
        if (ChainLengthField.Value is { } length && _chain is { } chain)
            Commit(document => document.SetChainLength(_undo!, _sequence, chain.Track, chain.Chain, (double)length));
    }

    private void ClearChainLength()
    {
        if (_refreshing) return;
        var chains = BulkChains() ?? (_chain is { } chain ? new[] { chain } : null);
        if (chains is not null)
            Commit(document => document.ClearChainsLength(_undo!, _sequence, chains));
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
