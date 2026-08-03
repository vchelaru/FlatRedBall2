using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Small HUD panel showing the currently tracked quest objective.
/// Press Q to cycle quests. Press J to open the full quest journal.
/// </summary>
public class QuestTracker
{
    private Screen _screen = null!;
    private Panel _trackerRoot = null!;
    private Label _questNameLabel = null!;
    private Label _objectiveLabel = null!;

    // Journal overlay
    private Panel _journalRoot = null!;
    private StackPanel _journalList = null!;
    private readonly List<Label> _journalLabels = [];
    private int _journalSelectedIndex;

    private QuestSystem _questSystem = null!;
    private int _trackedQuestIndex;
    private bool _journalOpen;

    public bool IsJournalOpen => _journalOpen;

    public event Action? JournalOpened;
    public event Action? JournalClosed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        // --- Small tracker HUD (top-left) ---
        _trackerRoot = new Panel();
        _trackerRoot.Anchor(Anchor.TopLeft);
        _trackerRoot.X = 8;
        _trackerRoot.Y = 8;
        _trackerRoot.Visual.Visible = false;

        var trackerBg = new ColoredRectangleRuntime
        {
            Width = 220, Height = 48,
            Red = 10, Green = 10, Blue = 20, Alpha = 180,
        };
        _trackerRoot.Visual.Children.Add(trackerBg);

        var trackerLayout = new StackPanel { Spacing = 2 };
        _questNameLabel = new Label { Text = "" };
        trackerLayout.AddChild(_questNameLabel);

        _objectiveLabel = new Label { Text = "" };
        trackerLayout.AddChild(_objectiveLabel);

        _trackerRoot.AddChild(trackerLayout);
        _screen.Add(_trackerRoot);

        // --- Full journal overlay ---
        _journalRoot = new Panel();
        _journalRoot.Dock(Dock.Fill);
        _journalRoot.Visual.Visible = false;

        var journalBg = new ColoredRectangleRuntime
        {
            Width = 0, Height = 0,
            WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            Red = 15, Green = 15, Blue = 30, Alpha = 230,
        };
        _journalRoot.Visual.Children.Add(journalBg);

        var journalTitle = new Label { Text = "-- Quest Journal --" };
        journalTitle.Anchor(Anchor.Top);
        journalTitle.Y = 8;
        _journalRoot.AddChild(journalTitle);

        _journalList = new StackPanel { Spacing = 4 };
        _journalList.Anchor(Anchor.TopLeft);
        _journalList.X = 16;
        _journalList.Y = 32;
        _journalRoot.AddChild(_journalList);

        var hint = new Label { Text = "Up/Down: select  Esc: close" };
        hint.Anchor(Anchor.BottomLeft);
        hint.X = 16;
        hint.Y = -8;
        _journalRoot.AddChild(hint);

        _screen.Add(_journalRoot);
    }

    public void Show(QuestSystem questSystem)
    {
        _questSystem = questSystem;
        _trackedQuestIndex = 0;
        RefreshTracker();
        _trackerRoot.Visual.Visible = true;
    }

    public void Hide()
    {
        _trackerRoot.Visual.Visible = false;
        _journalRoot.Visual.Visible = false;
        _journalOpen = false;
    }

    public void Update(FlatRedBallService engine)
    {
        var kb = engine.InputManager.Keyboard;

        if (_journalOpen)
        {
            UpdateJournal(kb);
            return;
        }

        if (kb.WasKeyPressed(Keys.Q))
        {
            CycleTrackedQuest();
        }
        else if (kb.WasKeyPressed(Keys.J))
        {
            OpenJournal();
        }

        // Refresh display each frame in case objectives updated
        RefreshTracker();
    }

    private void CycleTrackedQuest()
    {
        var quests = _questSystem.GetActiveQuests();
        if (quests.Count == 0) return;
        _trackedQuestIndex = (_trackedQuestIndex + 1) % quests.Count;
        RefreshTracker();
    }

    private void RefreshTracker()
    {
        var quests = _questSystem.GetActiveQuests();
        if (quests.Count == 0)
        {
            _questNameLabel.Text = "No active quests";
            _objectiveLabel.Text = "";
            return;
        }

        if (_trackedQuestIndex >= quests.Count)
            _trackedQuestIndex = 0;

        var quest = quests[_trackedQuestIndex];
        _questNameLabel.Text = quest.Name;

        if (quest.Objectives.Count > 0)
        {
            var obj = quest.Objectives[0];
            _objectiveLabel.Text = $"{FormatObjectiveType(obj.Type)} {obj.TargetId}: {obj.CurrentCount}/{obj.RequiredCount}";
        }
        else
        {
            _objectiveLabel.Text = "";
        }
    }

    private void OpenJournal()
    {
        _journalOpen = true;
        _journalSelectedIndex = 0;
        RebuildJournal();
        _journalRoot.Visual.Visible = true;
        JournalOpened?.Invoke();
    }

    private void UpdateJournal(FlatRedBall2.Input.IKeyboard kb)
    {
        if (kb.WasKeyPressed(Keys.Escape))
        {
            _journalOpen = false;
            _journalRoot.Visual.Visible = false;
            JournalClosed?.Invoke();
            return;
        }

        var totalEntries = GetJournalEntryCount();
        if (totalEntries == 0) return;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _journalSelectedIndex = (_journalSelectedIndex - 1 + totalEntries) % totalEntries;
            UpdateJournalHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _journalSelectedIndex = (_journalSelectedIndex + 1) % totalEntries;
            UpdateJournalHighlight();
        }
    }

    private void RebuildJournal()
    {
        foreach (var label in _journalLabels)
            _journalList.Visual.Children.Remove(label.Visual);
        _journalLabels.Clear();

        var active = _questSystem.GetActiveQuests();
        var completed = _questSystem.GetCompletedQuestIds();

        if (active.Count > 0)
        {
            AddJournalLabel("=== Active ===", isHeader: true);
            foreach (var quest in active)
            {
                string objectives = string.Join(", ",
                    quest.Objectives.Select(o => $"{o.CurrentCount}/{o.RequiredCount}"));
                AddJournalLabel($"  {quest.Name} [{objectives}]", isHeader: false);
            }
        }

        if (completed.Count > 0)
        {
            AddJournalLabel("=== Completed ===", isHeader: true);
            foreach (var id in completed)
                AddJournalLabel($"  {id} (done)", isHeader: false);
        }

        if (active.Count == 0 && completed.Count == 0)
            AddJournalLabel("No quests yet.", isHeader: false);

        UpdateJournalHighlight();
    }

    private void AddJournalLabel(string text, bool isHeader)
    {
        var label = new Label { Text = text };
        _journalList.AddChild(label);
        _journalLabels.Add(label);
    }

    private void UpdateJournalHighlight()
    {
        for (int i = 0; i < _journalLabels.Count; i++)
        {
            string text = _journalLabels[i].Text ?? "";
            // Strip existing prefix
            if (text.StartsWith("> ")) text = text[2..];
            else if (text.StartsWith("  ") && !text.StartsWith("===")) text = text.TrimStart();

            _journalLabels[i].Text = i == _journalSelectedIndex ? $"> {text}" : $"  {text}";
        }
    }

    private int GetJournalEntryCount() => _journalLabels.Count;

    private static string FormatObjectiveType(string type) => type switch
    {
        "defeat" => "Defeat",
        "collect" => "Collect",
        "talk_to" => "Talk to",
        "reach" => "Reach",
        _ => type,
    };
}
