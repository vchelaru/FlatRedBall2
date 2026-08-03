using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public enum MinigameType { Timing, Memory, Reaction }

public enum MinigamePhase { NotStarted, Active, Complete }

/// <summary>
/// Runs a short training minigame for pets. Max duration: 30 seconds.
/// Call Update each frame. When Phase == Complete, read Score and TrainingReward.
/// </summary>
public class TrainingMinigame
{
    private const float MaxDuration = 30f;
    private const int MinReward = 15;
    private const int MaxReward = 25;

    private readonly Random _random;

    public MinigameType Type { get; private set; }
    public MinigamePhase Phase { get; private set; }
    public float Elapsed { get; private set; }

    /// <summary>Normalized score from 0.0 (worst) to 1.0 (perfect).</summary>
    public float Score { get; private set; }

    /// <summary>Training stat reward (15-25) based on score.</summary>
    public float TrainingReward => MinReward + Score * (MaxReward - MinReward);

    // --- Timing game state ---
    private int _timingRound;
    private const int TimingRounds = 3;
    private float _markerPosition; // 0.0 to 1.0
    private float _markerSpeed;
    private float _targetStart; // target zone center
    private float _timingScoreSum;
    private bool _waitingForInput;

    // --- Memory game state ---
    private int _memoryRound;
    private const int MemoryRounds = 3;
    private List<int> _memorySequence = [];
    private int _memoryInputIndex;
    private float _memoryShowTimer;
    private bool _memoryShowingSequence;
    private int _memoryCorrectCount;

    // --- Reaction game state ---
    private int _reactionRound;
    private const int ReactionRounds = 5;
    private float _reactionWaitTimer;
    private float _reactionResponseTimer;
    private bool _reactionPromptActive;
    private float _reactionScoreSum;

    public TrainingMinigame(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public void Start(MinigameType type)
    {
        Type = type;
        Phase = MinigamePhase.Active;
        Elapsed = 0f;
        Score = 0f;

        switch (type)
        {
            case MinigameType.Timing:
                _timingRound = 0;
                _timingScoreSum = 0f;
                StartTimingRound();
                break;
            case MinigameType.Memory:
                _memoryRound = 0;
                _memoryCorrectCount = 0;
                StartMemoryRound();
                break;
            case MinigameType.Reaction:
                _reactionRound = 0;
                _reactionScoreSum = 0f;
                StartReactionWait();
                break;
        }
    }

    public void Update(float deltaSeconds)
    {
        if (Phase != MinigamePhase.Active) return;

        Elapsed += deltaSeconds;
        if (Elapsed >= MaxDuration)
        {
            Finish();
            return;
        }

        switch (Type)
        {
            case MinigameType.Timing:
                UpdateTiming(deltaSeconds);
                break;
            case MinigameType.Memory:
                UpdateMemory(deltaSeconds);
                break;
            case MinigameType.Reaction:
                UpdateReaction(deltaSeconds);
                break;
        }
    }

    /// <summary>Call when the player presses the action button.</summary>
    public void OnInput(int inputValue = 0)
    {
        if (Phase != MinigamePhase.Active) return;

        switch (Type)
        {
            case MinigameType.Timing:
                HandleTimingInput();
                break;
            case MinigameType.Memory:
                HandleMemoryInput(inputValue);
                break;
            case MinigameType.Reaction:
                HandleReactionInput();
                break;
        }
    }

    /// <summary>
    /// Applies the training reward to the given pet.
    /// </summary>
    public void ApplyReward(PetState pet)
    {
        if (Phase != MinigamePhase.Complete) return;
        pet.Training = Math.Min(100, pet.Training + TrainingReward);
    }

    // --- Timing game ---

    /// <summary>Current marker position (0-1) for UI display.</summary>
    public float TimingMarkerPosition => _markerPosition;

    /// <summary>Target zone center (0-1) for UI display.</summary>
    public float TimingTargetCenter => _targetStart;

    /// <summary>Target zone half-width for UI display.</summary>
    public float TimingTargetHalfWidth => 0.1f;

    public int CurrentRound => Type switch
    {
        MinigameType.Timing => _timingRound,
        MinigameType.Memory => _memoryRound,
        MinigameType.Reaction => _reactionRound,
        _ => 0
    };

    public bool IsReactionPromptActive => _reactionPromptActive;

    private void StartTimingRound()
    {
        _markerPosition = 0f;
        _markerSpeed = 0.8f + _timingRound * 0.2f;
        _targetStart = 0.3f + (float)_random.NextDouble() * 0.4f;
        _waitingForInput = true;
    }

    private void UpdateTiming(float deltaSeconds)
    {
        if (!_waitingForInput) return;
        _markerPosition += _markerSpeed * deltaSeconds;
        if (_markerPosition >= 1f)
        {
            // Missed — zero score for this round
            _timingRound++;
            if (_timingRound >= TimingRounds)
            {
                Score = _timingScoreSum / TimingRounds;
                Finish();
            }
            else
                StartTimingRound();
        }
    }

    private void HandleTimingInput()
    {
        if (!_waitingForInput) return;
        _waitingForInput = false;

        float distance = Math.Abs(_markerPosition - _targetStart);
        float accuracy = Math.Max(0f, 1f - distance / TimingTargetHalfWidth);
        _timingScoreSum += accuracy;
        _timingRound++;

        if (_timingRound >= TimingRounds)
        {
            Score = _timingScoreSum / TimingRounds;
            Finish();
        }
        else
            StartTimingRound();
    }

    // --- Memory game ---

    /// <summary>The sequence to memorize (indices 0-3 for 4 shapes).</summary>
    public IReadOnlyList<int> MemorySequence => _memorySequence;

    public bool IsShowingSequence => _memoryShowingSequence;

    private void StartMemoryRound()
    {
        int length = 4 + _memoryRound; // 4, 5, 6
        _memorySequence = [];
        for (int i = 0; i < length; i++)
            _memorySequence.Add(_random.Next(4));
        _memoryInputIndex = 0;
        _memoryShowTimer = length * 0.8f; // show for 0.8s per item
        _memoryShowingSequence = true;
    }

    private void UpdateMemory(float deltaSeconds)
    {
        if (_memoryShowingSequence)
        {
            _memoryShowTimer -= deltaSeconds;
            if (_memoryShowTimer <= 0)
                _memoryShowingSequence = false;
        }
    }

    private void HandleMemoryInput(int inputValue)
    {
        if (_memoryShowingSequence) return;
        if (_memoryInputIndex >= _memorySequence.Count) return;

        if (inputValue == _memorySequence[_memoryInputIndex])
            _memoryCorrectCount++;

        _memoryInputIndex++;
        if (_memoryInputIndex >= _memorySequence.Count)
        {
            _memoryRound++;
            if (_memoryRound >= MemoryRounds)
            {
                int totalItems = Enumerable.Range(0, MemoryRounds).Sum(r => 4 + r);
                Score = (float)_memoryCorrectCount / totalItems;
                Finish();
            }
            else
                StartMemoryRound();
        }
    }

    // --- Reaction game ---

    private void StartReactionWait()
    {
        _reactionPromptActive = false;
        _reactionWaitTimer = 1f + (float)_random.NextDouble() * 2f; // 1-3s delay
        _reactionResponseTimer = 0f;
    }

    private void UpdateReaction(float deltaSeconds)
    {
        if (!_reactionPromptActive)
        {
            _reactionWaitTimer -= deltaSeconds;
            if (_reactionWaitTimer <= 0)
            {
                _reactionPromptActive = true;
                _reactionResponseTimer = 0f;
            }
        }
        else
        {
            _reactionResponseTimer += deltaSeconds;
            // Auto-fail after 2 seconds
            if (_reactionResponseTimer >= 2f)
            {
                _reactionRound++;
                if (_reactionRound >= ReactionRounds)
                {
                    Score = _reactionScoreSum / ReactionRounds;
                    Finish();
                }
                else
                    StartReactionWait();
            }
        }
    }

    private void HandleReactionInput()
    {
        if (!_reactionPromptActive) return;

        // Score based on reaction time: 0.0s = 1.0 score, 1.0s+ = 0.0 score
        float reactionScore = Math.Max(0f, 1f - _reactionResponseTimer);
        _reactionScoreSum += reactionScore;
        _reactionRound++;

        if (_reactionRound >= ReactionRounds)
        {
            Score = _reactionScoreSum / ReactionRounds;
            Finish();
        }
        else
            StartReactionWait();
    }

    private void Finish()
    {
        Phase = MinigamePhase.Complete;
        Score = Math.Clamp(Score, 0f, 1f);
    }
}
