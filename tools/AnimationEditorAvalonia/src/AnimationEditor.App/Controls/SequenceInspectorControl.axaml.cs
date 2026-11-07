using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The Inspector tab's content on a <c>.seqj</c> tab: the sequence when nothing is selected on the timeline, a track
/// when its header is, a chain when one is. Every commit is one edit on the <see cref="SequenceDocument"/>, so one undo
/// entry; a refused value (an empty or taken name, loops of 0) goes out through <see cref="ErrorRaised"/> and the field
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
    private bool _refreshing;

    public SequenceInspectorControl()
    {
        InitializeComponent();
        CommitOnEnterOrFocusLost(SequenceNameBox, CommitSequenceName);
        CommitOnEnterOrFocusLost(TrackNameBox, CommitTrackName);
        SequenceLoopBox.IsCheckedChanged += (_, _) => Commit(document =>
            document.SetLoop(_undo!, _sequence, SequenceLoopBox.IsChecked == true));

        TrackXField.ValueChanged += (_, _) => CommitOffset();
        TrackYField.ValueChanged += (_, _) => CommitOffset();
        TrackFlipBox.IsCheckedChanged += (_, _) => Commit(document =>
            document.SetTrackFlip(_undo!, _sequence, _track!.Value, TrackFlipBox.IsChecked == true));
        TrackZField.ValueChanged += (_, _) =>
        {
            if (TrackZField.Value is { } z)
                Commit(document => document.SetTrackZ(_undo!, _sequence, _track!.Value, (int)z));
        };

        ChainStartField.ValueChanged += (_, _) =>
        {
            if (ChainStartField.Value is { } start && _chain is { } chain)
                Commit(document => document.SetChainStart(_undo!, _sequence, chain.Track, chain.Chain, (double)start));
        };
        ChainLoopsField.ValueChanged += (_, _) =>
        {
            if (ChainLoopsField.Value is { } loops && _chain is { } chain)
                Commit(document => document.SetChainLoops(_undo!, _sequence, chain.Track, chain.Chain, (float)loops));
        };
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
    /// or (both null) the sequence itself. A null document clears the inspector.
    /// </summary>
    public void Show(SequenceDocument? document, int sequence, IUndoManager? undo, TimelineChainId? chain, int? track)
    {
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
        SequenceLoopBox.IsChecked = sequence.Loop;
        SequenceLengthText.Text = Seconds(SequenceTimeline.Length(sequence, reference => document.CycleLength(reference) ?? 0));
        SequenceTracksText.Text = sequence.Tracks.Count.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowTrack(SequenceTrackSave track, bool force)
    {
        Put(TrackNameBox, track.Name, force);
        Put(TrackXField, (decimal)track.X, force);
        Put(TrackYField, (decimal)track.Y, force);
        TrackFlipBox.IsChecked = track.FlipHorizontal;
        Put(TrackZField, track.Z, force);
    }

    private void ShowChain(SequenceChainReferenceSave reference, bool force)
    {
        var document = _document!;
        ChainSourceText.Text = reference.Source;
        ToolTip.SetTip(ChainSourceText, reference.Source);
        ChainNameText.Text = reference.Chain;
        ToolTip.SetTip(ChainNameText, reference.Chain);
        Put(ChainStartField, (decimal)reference.Start, force);
        Put(ChainLoopsField, (decimal)reference.Loops, force);
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

    private static void Put(FlankerNumericField field, decimal value, bool force)
    {
        var box = field.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        string shown = field.Value?.ToString(field.FormatString, CultureInfo.InvariantCulture) ?? string.Empty;
        bool typing = field.IsKeyboardFocusWithin && box is not null && box.Text != shown;
        if (force || !typing) field.Value = value;
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

    private void CommitOffset()
    {
        if (_refreshing || _track is not { } track || TrackXField.Value is not { } x || TrackYField.Value is not { } y) return;
        Commit(document => document.SetTrackOffset(_undo!, _sequence, track, (float)x, (float)y));
    }

    private string? CommitSequenceName(SequenceDocument document, string text) => CurrentSequence is null
        ? null
        : document.RenameSequence(_undo!, _sequence, text);

    private string? CommitTrackName(SequenceDocument document, string text) => _track is { } track
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
