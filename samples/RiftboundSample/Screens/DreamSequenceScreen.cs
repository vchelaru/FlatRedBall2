using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Systems;
using RiftboundSample.UI;

namespace RiftboundSample.Screens;

/// <summary>
/// Non-combat narration showing Lira's perspective in the Ethereal realm.
/// Loads a dialogue file, displays it via DialogueBox, then returns to OverworldScreen.
/// </summary>
public class DreamSequenceScreen : Screen
{
    private DialogueSystem _dialogueSystem = new();
    private DialogueBox _dialogueBox = new();
    private bool _dialogueStarted;

    /// <summary>Dialogue file to load (relative path, e.g. "Data/dialogue/dreams/dream1.json").</summary>
    public string DialogueFile { get; set; } = "";

    /// <summary>Map to return to after the dream sequence.</summary>
    public string? ReturnMapId { get; set; }

    /// <summary>Player position to restore on return.</summary>
    public float? RestorePlayerX { get; set; }
    public float? RestorePlayerY { get; set; }

    /// <summary>Dream sequence ID for cutscene replay tracking.</summary>
    public string? DreamId { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(5, 3, 15);

        // Title hint
        var hint = new Label { Text = "~ Dream ~" };
        hint.Anchor(Anchor.Top);
        hint.Y = 20;
        Add(hint);

        // Load dialogue
        if (!string.IsNullOrEmpty(DialogueFile) && File.Exists(DataPath.Resolve(DialogueFile)))
        {
            _dialogueSystem.LoadFromFile(DialogueFile);
        }

        _dialogueBox.Initialize(this);
        _dialogueBox.DialogueEnded += OnDreamEnded;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_dialogueStarted)
        {
            // Start the first node in the loaded file
            // Convention: dream dialogues start with "{dreamId}_1" or the first node ID
            string startId = DreamId != null ? $"{DreamId}_1" : "dream_1";
            _dialogueSystem.StartDialogue(startId);

            if (_dialogueSystem.IsActive)
            {
                _dialogueBox.Show(_dialogueSystem);
                _dialogueStarted = true;
            }
            else
            {
                // No valid dialogue — return immediately
                ReturnToOverworld();
                return;
            }
        }

        _dialogueBox.Update(Engine);
    }

    private void OnDreamEnded()
    {
        ReturnToOverworld();
    }

    private void ReturnToOverworld()
    {
        MoveToScreen<OverworldScreen>(s =>
        {
            s.InitialMapId = ReturnMapId;
            s.RestorePlayerX = RestorePlayerX;
            s.RestorePlayerY = RestorePlayerY;
        });
    }
}
