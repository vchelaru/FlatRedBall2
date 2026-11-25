using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
/// (including each step of a drag), which rewrites every field except the one being typed in. Typed text only
/// ever applies to what it was typed for: selecting another item commits it there first, and it is dropped when a change
/// outside the editor or an undo left another item in its place.
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
    private Shown? _shown;

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
        var before = _shown;
        _chains = chains ?? Array.Empty<TimelineChainId>();
        _tracks = tracks ?? Array.Empty<int>();
        _canRenameSequence = canRenameSequence;
        _document = document;
        _sequence = sequence;
        _undo = undo;
        _chain = chain;
        _track = track;
        _shown = Snapshot();
        if (TypingField() is { } field && !SameItem(field, before, _shown))
        {
            string text = TextOf(field);
            DiscardTypedText();
            // The data is the one the text was typed over, so the user selected something else: the text goes to the item it
            // was typed for, as a focus loss would commit it. After any other change that item is not the one shown any more.
            if (before is not null && _shown is not null && ReferenceEquals(before.Save, _shown.Save))
                CommitLater(field, text, before);
        }
        Refresh(force: false);
    }

    // What the fields edit and everything around it that tells whether a later Show still shows the same thing: a change
    // outside the editor, or an undo, can put another item at an index.
    private sealed record Shown(SequenceDocument Document, SequenceListSave Save, int Sequence, TimelineChainId? Chain, int? Track,
        IReadOnlyList<TimelineChainId> Chains, IReadOnlyList<int> Tracks, bool CanRenameSequence,
        IReadOnlyList<string> SequenceNames, IReadOnlyList<ShownTrack> TrackData)
    {
        public IReadOnlyList<string> TrackNames { get; } = TrackData.Select(track => track.Name).ToList();

        // The selected blocks, primary included.
        public IReadOnlyList<TimelineChainId> Blocks { get; } = (Chain is { } chain ? Chains.Append(chain) : Chains).Distinct().ToList();

        public ShownTrack? TrackAt(int index) => index >= 0 && index < TrackData.Count ? TrackData[index] : null;
    }

    private sealed record ShownTrack(string Name, IReadOnlyList<(string Source, string Chain, double Start, double? Length)> Blocks,
        IReadOnlyList<(string Sequence, double Start, double? Length)> Clips, int FirstClip);

    private Shown? Snapshot()
    {
        if (_document is not { } document || CurrentSequence is not { } sequence) return null;
        var tracks = sequence.Tracks.Select(track => new ShownTrack(track.Name,
            track.Chains.Select(block => (block.Source, block.Chain, block.Start, block.Length)).ToList(),
            (track.References ?? []).Select(clip => (clip.Sequence, clip.Start, clip.Length)).ToList(),
            SequenceTimeline.OrderedClips(track).FirstOrDefault() is { } first ? track.References!.IndexOf(first) : -1)).ToList();
        return new Shown(document, document.Save, _sequence, _chain, _track, _chains, _tracks, _canRenameSequence,
            document.Save.Sequences.Select(s => s.Name).ToList(), tracks);
    }

    // Whether what field edits is the same item in both. Its other values may have changed (an undo, an edit outside).
    private bool SameItem(Control field, Shown? before, Shown? now)
    {
        if (before is null || now is null || !(ReferenceEquals(before.Document, now.Document) || before.Document.Path == now.Document.Path))
            return false;
        if (field == SequenceNameBox)
            return Same(before.SequenceNames, before.Sequence, now.SequenceNames, now.Sequence, (_, _) => false)
                && before.Blocks.Count + now.Blocks.Count == 0 && before.Track is null && now.Track is null;
        if (!Same(before.SequenceNames, before.Sequence, now.SequenceNames, now.Sequence, (_, _) => true)) return false;
        bool SameTrack(int from, int to, bool renamed) =>
            before.TrackAt(from) is not null && now.TrackAt(to) is not null && Same(before.TrackNames, from, now.TrackNames, to, (_, _) => renamed);

        if (field == ChainStartField || field == ChainLengthField)
        {
            return before.Blocks.Count > 0 && before.Blocks.Count == now.Blocks.Count && before.Blocks.Zip(now.Blocks).All(pair =>
                SameTrack(pair.First.Track, pair.Second.Track, renamed: true)
                && Same(before.TrackData[pair.First.Track].Blocks, pair.First.Chain, now.TrackData[pair.Second.Track].Blocks, pair.Second.Chain,
                    (was, isNow) => was.Source == isNow.Source && was.Chain == isNow.Chain));
        }
        if (before.Blocks.Count + now.Blocks.Count > 0 || before.Track is not { } fromTrack || now.Track is not { } toTrack) return false;
        if (field == TrackNameBox) return SameTrack(fromTrack, toTrack, renamed: false);
        return SameTrack(fromTrack, toTrack, renamed: true)
            && Same(before.TrackData[fromTrack].Clips, before.TrackData[fromTrack].FirstClip, now.TrackData[toTrack].Clips, now.TrackData[toTrack].FirstClip,
                (was, isNow) => was.Sequence == isNow.Sequence);
    }

    // Items have no identity of their own: the same item has the same content (and no double that could be it instead), or
    // is at the same place of a list no item came or went from, with its content edited.
    private static bool Same<T>(IReadOnlyList<T> before, int from, IReadOnlyList<T> now, int to, Func<T, T, bool> edited)
    {
        if (from < 0 || from >= before.Count || to < 0 || to >= now.Count) return false;
        bool samePlace = from == to && before.Count == now.Count;
        if (Equals(before[from], now[to]))
            return samePlace || (before.Count(item => Equals(item, before[from])) == 1 && now.Count(item => Equals(item, before[from])) == 1);
        return samePlace && !now.Contains(before[from]) && edited(before[from], now[to]);
    }

    // The field that has the focus and holds text not committed yet, or null.
    private Control? TypingField()
    {
        foreach (var box in new[] { SequenceNameBox, TrackNameBox })
            if (box.IsKeyboardFocusWithin && !Equals(box.Tag, box.Text)) return box;
        return NumberFields.FirstOrDefault(field => field.IsKeyboardFocusWithin && field.HasTypedText);
    }

    private FlankerNumericField[] NumberFields => [ChainStartField, ChainLengthField, ReferenceStartField, ReferenceLengthField];

    private static string TextOf(Control field) => field is FlankerNumericField number ? number.TypedText : ((TextBox)field).Text ?? string.Empty;

    /// <summary>
    /// Applies the text typed into the field that has the focus, as Enter would: for a way of leaving it that takes no focus (a
    /// tab switch, a hotkey). Does nothing when none was typed.
    /// </summary>
    public void CommitTypedText()
    {
        if (TypingField() is { } field) CommitText(field, TextOf(field));
    }

    private void CommitText(Control field, string text)
    {
        if (field is FlankerNumericField number) number.CommitText(text);
        else if (field == SequenceNameBox) Commit(document => CommitSequenceName(document, text));
        else Commit(document => CommitTrackName(document, text));
        _undo?.SealCoalescing();
    }

    // Waits for the click that selected the other item to finish: an edit rebuilds the list that click is still being handled by.
    private void CommitLater(Control field, string text, Shown target) => Dispatcher.UIThread.Post(() =>
    {
        if (_shown is not { } current || !ReferenceEquals(current.Save, target.Save)) return;
        target.Document.KeepSelection(() =>
        {
            Show(target);
            CommitText(field, text);
        });
        Show(current);
    });

    private void Show(Shown item) => Show(item.Document, item.Sequence, _undo, item.Chain, item.Track, item.Chains, item.Tracks, item.CanRenameSequence);

    private void DiscardTypedText()
    {
        _refreshing = true;
        try
        {
            SequenceNameBox.Text = SequenceNameBox.Tag as string;
            TrackNameBox.Text = TrackNameBox.Tag as string;
            foreach (var field in NumberFields) field.DiscardTypedText();
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
