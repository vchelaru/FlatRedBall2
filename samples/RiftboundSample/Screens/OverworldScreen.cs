using System.Diagnostics;
using System.Text.Json;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Entities;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Entities;
using RiftboundSample.Levels;
using RiftboundSample.Models;
using RiftboundSample.Systems;
using RiftboundSample.UI;

namespace RiftboundSample.Screens;

public class OverworldScreen : Screen
{
    // Factories
    private Factory<PlayerEntity> _playerFactory = null!;
    private Factory<OverworldEnemyEntity> _enemyFactory = null!;
    private Factory<MarkerEntity> _markerFactory = null!;
    private Factory<CameraControllingEntity> _cameraFactory = null!;

    // References
    private PlayerEntity _player = null!;
    private LoadedMap _loadedMap = null!;

    // Pet care (preserved from Milestone 1)
    private PetCarePanel _petCarePanel = new();
    private PetCareSystem _petCareSystem = null!;
    private List<PetState> _pets = [];

    // Dialogue
    private DialogueSystem _dialogueSystem = new();
    private DialogueBox _dialogueBox = new();

    // Shop
    private ShopSystem _shopSystem = null!;
    private ShopPanel _shopPanel = new();

    // Save/Load
    private SaveLoadPanel _saveLoadPanel = new();

    // Pause
    private PauseMenu _pauseMenu = new();

    // Data
    private PartyState _partyState = new();
    private Dictionary<string, EquipmentData> _equipmentLookup = [];
    private Dictionary<string, CharacterData> _characterLookup = [];
    private DateTime _sessionStart;

    // Minimap
    private MinimapPanel _minimap = null!;

    // Screen transitions
    private ScreenTransitionEffect _transition = new();

    // Inn
    private InnPanel _innPanel = new();

    // Fast travel
    private FastTravelSystem _fastTravel = new();
    private FastTravelPanel _fastTravelPanel = new();

    // Rift tear puzzle
    private RiftTearPuzzlePanel _riftTearPanel = new();
    private List<RiftTearData> _riftTears = [];

    // Story events
    private StoryEventSystem _storyEventSystem = new();
    private HashSet<string> _flags = [];
    private TutorialSystem _tutorialSystem = null!;
    private CutsceneReplaySystem _cutsceneReplay = new();
    private StoryEvent? _pendingStoryEvent;

    // Text log & cutscene replay UI
    private TextLogPanel _textLogPanel = new();
    private CutsceneReplayPanel _cutsceneReplayPanel = new();

    // UI state tracking
    private enum OverlayState { None, PetCare, Dialogue, Shop, SaveLoad, Paused, Inn, FastTravel, RiftTear, TextLog, CutsceneReplay }
    private OverlayState _overlay = OverlayState.None;

    // Map tracking
    private string _currentMapId = "brasshollow";

    // Position tracking for battle return
    private float _lastPlayerX;
    private float _lastPlayerY;

    // Restore position after battle
    public float? RestorePlayerX { get; set; }
    public float? RestorePlayerY { get; set; }

    /// <summary>Set before transitioning to load a specific map.</summary>
    public string? InitialMapId { get; set; }

    public override DisplaySettings? PreferredDisplaySettings => null;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 10, 15);
        _sessionStart = DateTime.Now;

        // Create factories
        _playerFactory = new Factory<PlayerEntity>(this);
        _enemyFactory = new Factory<OverworldEnemyEntity>(this);
        _markerFactory = new Factory<MarkerEntity>(this);
        _cameraFactory = new Factory<CameraControllingEntity>(this);

        // Load the map
        _currentMapId = InitialMapId ?? "brasshollow";
        var mapData = MapRegistry.Get(_currentMapId);
        var theme = MapRegistry.GetTheme(_currentMapId);
        _loadedMap = MapLoader.Load(mapData, this, theme);

        // Spawn player
        _player = _playerFactory.Create();
        if (RestorePlayerX.HasValue && RestorePlayerY.HasValue)
        {
            _player.X = RestorePlayerX.Value;
            _player.Y = RestorePlayerY.Value;
        }
        else
        {
            _player.X = _loadedMap.PlayerStart.X;
            _player.Y = _loadedMap.PlayerStart.Y;
        }

        // Spawn enemies
        var (areaMinLevel, _) = ProgressionSystem.AreaLevelRange(_currentMapId);
        foreach (var (ex, ey) in _loadedMap.EnemySpawns)
        {
            var enemy = _enemyFactory.Create();
            enemy.X = ex;
            enemy.Y = ey;
            enemy.EnemyGroupId = "gear_golem";
            enemy.AreaLevel = areaMinLevel;
            enemy.InitializePatrol();
        }

        // Spawn NPC markers (yellow)
        foreach (var (nx, ny) in _loadedMap.NpcPositions)
        {
            var npc = _markerFactory.Create();
            npc.X = nx;
            npc.Y = ny;
            npc.MarkerColor = new Color(220, 200, 50);
            npc.MarkerType = "NPC";
        }

        // Spawn door markers (blue)
        foreach (var (dx, dy) in _loadedMap.DoorPositions)
        {
            var door = _markerFactory.Create();
            door.X = dx;
            door.Y = dy;
            door.MarkerColor = new Color(60, 100, 220);
            door.MarkerType = "Door";
        }

        // Spawn shop markers (orange)
        foreach (var (sx, sy) in _loadedMap.ShopPositions)
        {
            var shop = _markerFactory.Create();
            shop.X = sx;
            shop.Y = sy;
            shop.MarkerColor = new Color(220, 160, 40);
            shop.MarkerType = "Shop";
        }

        // Spawn inn markers (purple)
        foreach (var (ix, iy) in _loadedMap.InnPositions)
        {
            var inn = _markerFactory.Create();
            inn.X = ix;
            inn.Y = iy;
            inn.MarkerColor = new Color(160, 80, 200);
            inn.MarkerType = "Inn";
        }

        // Spawn boss door markers (red)
        foreach (var (bx, by) in _loadedMap.BossDoorPositions)
        {
            var bossDoor = _markerFactory.Create();
            bossDoor.X = bx;
            bossDoor.Y = by;
            bossDoor.MarkerColor = new Color(200, 40, 40);
            bossDoor.MarkerType = "Door";
        }

        // Spawn rift tear markers (cyan)
        foreach (var (rx, ry) in _loadedMap.RiftTearPositions)
        {
            var rift = _markerFactory.Create();
            rift.X = rx;
            rift.Y = ry;
            rift.MarkerColor = new Color(0, 220, 220);
            rift.MarkerType = "RiftTear";
        }

        // Spawn colosseum markers (gold)
        foreach (var (cx, cy) in _loadedMap.ColosseumPositions)
        {
            var col = _markerFactory.Create();
            col.X = cx;
            col.Y = cy;
            col.MarkerColor = new Color(220, 180, 40);
            col.MarkerType = "Colosseum";
        }

        // Collision: player vs walls
        AddCollisionRelationship(_playerFactory, _loadedMap.Walls)
            .MoveFirstOnCollision();

        // Collision: enemies vs walls
        AddCollisionRelationship(_enemyFactory, _loadedMap.Walls)
            .MoveFirstOnCollision();

        // Collision: player vs enemies (trigger battle)
        AddCollisionRelationship<PlayerEntity, OverworldEnemyEntity>(_playerFactory, _enemyFactory)
            .CollisionOccurred += OnPlayerTouchedEnemy;

        // Camera setup
        var cam = _cameraFactory.Create();
        cam.Target = _player;
        cam.Map = new AxisAlignedRectangle
        {
            Width = _loadedMap.MapWidth,
            Height = _loadedMap.MapHeight,
        };
        cam.TargetApproachStyle = TargetApproachStyle.Smooth;
        cam.TargetApproachCoefficient = 8f;

        // Pet care
        LoadPetData();
        _petCarePanel.Initialize(this);
        _petCarePanel.Closed += () => _overlay = OverlayState.None;

        // Load additional data for town systems
        LoadTownData();

        // Dialogue
        _dialogueBox.Initialize(this);
        _dialogueBox.DialogueEnded += OnDialogueEnded;

        // Shop
        _shopPanel.Initialize(this);
        _shopPanel.Closed += () => _overlay = OverlayState.None;

        // Save/Load
        _saveLoadPanel.Initialize(this);
        _saveLoadPanel.Closed += () =>
        {
            _overlay = OverlayState.Paused;
            _pauseMenu.Show();
        };
        _saveLoadPanel.LoadRequested += OnLoadRequested;
        _saveLoadPanel.SaveDataProvider = BuildSaveData;

        // Pause
        _pauseMenu.Initialize(this);
        _pauseMenu.ResumeSelected += () => _overlay = OverlayState.None;
        _pauseMenu.SaveSelected += () =>
        {
            _overlay = OverlayState.SaveLoad;
            _saveLoadPanel.Show(saveMode: true);
        };
        _pauseMenu.LoadSelected += () =>
        {
            _overlay = OverlayState.SaveLoad;
            _saveLoadPanel.Show(saveMode: false);
        };
        _pauseMenu.TextLogSelected += () =>
        {
            _overlay = OverlayState.TextLog;
            _textLogPanel.Show(_dialogueSystem.Log);
        };
        _pauseMenu.CutscenesSelected += () =>
        {
            _overlay = OverlayState.CutsceneReplay;
            _cutsceneReplayPanel.Show(_cutsceneReplay);
        };
        _pauseMenu.QuitSelected += () => MoveToScreen<TitleScreen>();

        // Rift tear puzzle
        LoadRiftTearData();
        _riftTearPanel.Initialize(this);
        _riftTearPanel.PuzzleSolved += OnRiftTearSolved;
        _riftTearPanel.Closed += () => _overlay = OverlayState.None;

        // Inn
        _innPanel.Initialize(this);
        _innPanel.Closed += () => _overlay = OverlayState.None;

        // Fast travel
        _fastTravel.MarkVisited(_currentMapId);
        _fastTravelPanel.Initialize(this);
        _fastTravelPanel.Closed += () => _overlay = OverlayState.None;
        _fastTravelPanel.MapSelected += OnFastTravelSelected;

        // Text log panel
        _textLogPanel.Initialize(this);
        _textLogPanel.Closed += () =>
        {
            _overlay = OverlayState.Paused;
            _pauseMenu.Show();
        };

        // Cutscene replay panel
        _cutsceneReplayPanel.Initialize(this);
        _cutsceneReplayPanel.Closed += () =>
        {
            _overlay = OverlayState.Paused;
            _pauseMenu.Show();
        };
        _cutsceneReplayPanel.ReplaySelected += OnCutsceneReplaySelected;

        // Story event system
        _storyEventSystem.LoadFromFile("Data/story_events.json");
        _tutorialSystem = new TutorialSystem(_flags);

        // Minimap
        _minimap = new MinimapPanel();
        _minimap.Initialize(this, _loadedMap);

        // Screen transition (fade in from black)
        _transition.Initialize(this);
        _transition.Start(TransitionType.FadeToBlack, 0.6f);

        // Check for story events on map entry
        CheckStoryEvent("map_enter", _currentMapId);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Update transition overlay
        _transition.Update(time.DeltaSeconds);

        // Update pet stat decay always
        _petCareSystem.Update(time.DeltaSeconds, _pets);

        // Dispatch to active overlay
        switch (_overlay)
        {
            case OverlayState.PetCare:
                _petCarePanel.Update(Engine);
                return;
            case OverlayState.Dialogue:
                _dialogueBox.Update(Engine);
                return;
            case OverlayState.Shop:
                _shopPanel.Update(Engine);
                return;
            case OverlayState.SaveLoad:
                _saveLoadPanel.Update(Engine);
                return;
            case OverlayState.Paused:
                _pauseMenu.Update(Engine);
                return;
            case OverlayState.Inn:
                _innPanel.Update(Engine);
                return;
            case OverlayState.FastTravel:
                _fastTravelPanel.Update(Engine);
                return;
            case OverlayState.RiftTear:
                _riftTearPanel.Update(Engine);
                return;
            case OverlayState.TextLog:
                _textLogPanel.Update(Engine, _dialogueSystem.Log);
                return;
            case OverlayState.CutsceneReplay:
                _cutsceneReplayPanel.Update(Engine);
                return;
        }

        // Free-roam input
        var kb = Engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            _overlay = OverlayState.Paused;
            _pauseMenu.Show();
            return;
        }

        if (kb.WasKeyPressed(Keys.P) && _pets.Count > 0)
        {
            _overlay = OverlayState.PetCare;
            _petCarePanel.Show(_pets[0], _petCareSystem);
            return;
        }

        if (kb.WasKeyPressed(Keys.T))
        {
            _overlay = OverlayState.FastTravel;
            _fastTravelPanel.Show(_fastTravel);
            return;
        }

        // NPC interaction (Enter key when near)
        if (kb.WasKeyPressed(Keys.Enter))
            TryInteractWithNearbyMarker();

        // Enemy flee behavior: outleveled enemies flee from the player
        UpdateEnemyFleeBehavior();

        // Update minimap
        _minimap.Update(_player, _enemyFactory, _markerFactory);
    }

    private void OnPlayerTouchedEnemy(PlayerEntity player, OverworldEnemyEntity enemy)
    {
        Debug.WriteLine($"Battle triggered! Enemy group: {enemy.EnemyGroupId}");

        // Store player position so BattleScreen can pass it back on victory
        _lastPlayerX = player.X;
        _lastPlayerY = player.Y;

        // Autosave before entering battle
        SaveSystem.Autosave(BuildSaveData());

        MoveToScreen<BattleScreen>(b => b.ReturnPlayerPosition = (_lastPlayerX, _lastPlayerY));
    }

    private void TryInteractWithNearbyMarker()
    {
        const float interactRange = 20f;

        foreach (var marker in _markerFactory)
        {
            float dx = marker.X - _player.X;
            float dy = marker.Y - _player.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq < interactRange * interactRange)
            {
                switch (marker.MarkerType)
                {
                    case "NPC":
                        _overlay = OverlayState.Dialogue;
                        _dialogueSystem.StartDialogue("elder_1");
                        _dialogueBox.Show(_dialogueSystem);
                        break;
                    case "Door":
                        TryTransitionThroughDoor(marker.X, marker.Y);
                        return;
                    case "Shop":
                        _overlay = OverlayState.Shop;
                        string activeChar = _partyState.ActiveParty.Count > 0 ? _partyState.ActiveParty[0] : "";
                        _shopPanel.Show(
                            _shopSystem, _partyState, activeChar,
                            _characterLookup, _equipmentLookup.Values.ToList());
                        break;
                    case "Inn":
                        _overlay = OverlayState.Inn;
                        _innPanel.Show(_partyState, _characterLookup, _currentMapId, _petCarePanel, _petCareSystem, _pets);
                        break;
                    case "RiftTear":
                        TryOpenRiftTear();
                        break;
                    case "Colosseum":
                        MoveToScreen<ColosseumScreen>();
                        return;
                }
                return;
            }
        }
    }

    private void TryTransitionThroughDoor(float doorX, float doorY)
    {
        if (_loadedMap.DoorTargetLookup.TryGetValue((doorX, doorY), out var targetMapId))
        {
            Debug.WriteLine($"Transitioning from {_currentMapId} to {targetMapId}");
            MoveToScreen<OverworldScreen>(s =>
            {
                s.InitialMapId = targetMapId;
                // Preserve party state across transitions by passing position
                // (full state persistence would use save data)
            });
        }
        else
        {
            Debug.WriteLine("Door: No target map configured for this door.");
        }
    }

    private void LoadPetData()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        string petJson = File.ReadAllText(DataPath.Resolve("Data/pets.json"));
        var petDataList = JsonSerializer.Deserialize<List<PetData>>(petJson, options) ?? [];
        var petDataLookup = petDataList.ToDictionary(p => p.Id);

        _pets = petDataList.Select(PetState.FromData).ToList();
        _petCareSystem = new PetCareSystem(petDataLookup, Engine.Random);
    }

    private void LoadTownData()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Load characters for party state
        string charJson = File.ReadAllText(DataPath.Resolve("Data/characters.json"));
        var characters = JsonSerializer.Deserialize<List<CharacterData>>(charJson, options) ?? [];
        _characterLookup = characters.ToDictionary(c => c.Id);

        // Load equipment data
        string equipJson = File.ReadAllText(DataPath.Resolve("Data/equipment.json"));
        var equipList = JsonSerializer.Deserialize<List<EquipmentData>>(equipJson, options) ?? [];
        _equipmentLookup = equipList.ToDictionary(e => e.Id);

        // Load dialogue for all areas
        _dialogueSystem.LoadFromFile("Data/dialogue/brasshollow_npcs.json");
        _dialogueSystem.LoadFromFile("Data/dialogue/ethereal_npcs.json");

        // Initialize shop
        _shopSystem = new ShopSystem(_equipmentLookup);
        _shopSystem.LoadShop("Data/shops/brasshollow_shop.json");

        // Initialize party state with starting values
        _partyState.Gold = 300;
        _partyState.Roster = characters.Select(c => c.Id).ToList();
        _partyState.ActiveParty = characters.Select(c => c.Id).ToList();
        _partyState.Pets = _pets;

        // Equip starting gear
        _partyState.Equip("kael", "weapon", "rusty_wrench");
        _partyState.Equip("mira", "armor", "old_robes");
    }

    private void UpdateEnemyFleeBehavior()
    {
        float avgLevel = GetPartyAverageLevel();

        foreach (var enemy in _enemyFactory)
        {
            float dx = enemy.X - _player.X;
            float dy = enemy.Y - _player.Y;
            float distSq = dx * dx + dy * dy;

            bool isOutleveled = avgLevel >= enemy.AreaLevel + 5;

            if (isOutleveled && distSq < 80f * 80f)
            {
                enemy.StartFleeing(_player.X, _player.Y);
                enemy.UpdateFleeDirection(_player.X, _player.Y);
            }
            else if (enemy.IsFleeing && distSq > 120f * 120f)
            {
                enemy.StopFleeing();
            }
            else if (enemy.IsFleeing)
            {
                enemy.UpdateFleeDirection(_player.X, _player.Y);
            }
        }
    }

    private float GetPartyAverageLevel()
    {
        if (_partyState.ActiveParty.Count == 0) return 1;

        float totalLevel = 0;
        int count = 0;
        foreach (var id in _partyState.ActiveParty)
        {
            if (_characterLookup.TryGetValue(id, out var data))
            {
                totalLevel += data.Level;
                count++;
            }
        }
        return count > 0 ? totalLevel / count : 1;
    }

    private void LoadRiftTearData()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string json = File.ReadAllText(DataPath.Resolve("Data/rift_tears.json"));
        _riftTears = JsonSerializer.Deserialize<List<RiftTearData>>(json, options) ?? [];
    }

    private void TryOpenRiftTear()
    {
        var tearData = _riftTears.FirstOrDefault(r => r.Location == _currentMapId);
        if (tearData == null)
        {
            Debug.WriteLine("No rift tear data found for this map.");
            return;
        }

        var puzzle = RiftTearPuzzle.Generate(tearData.Difficulty, Engine.Random);
        _overlay = OverlayState.RiftTear;
        _riftTearPanel.Show(puzzle, tearData);
    }

    private void OnRiftTearSolved(RiftTearData tearData)
    {
        Debug.WriteLine($"Rift tear solved! Reward: {tearData.Reward.ItemId} x{tearData.Reward.Count}");

        if (tearData.Reward.Type == "item")
            _partyState.AddItem(tearData.Reward.ItemId, tearData.Reward.Count);
        else if (tearData.Reward.Type == "recipe")
            Debug.WriteLine($"Recipe unlocked: {tearData.Reward.ItemId}");
    }

    private void OnFastTravelSelected(string mapId)
    {
        _overlay = OverlayState.None;
        MoveToScreen<OverworldScreen>(s => s.InitialMapId = mapId);
    }

    private void CheckStoryEvent(string triggerType, string triggerValue)
    {
        var evt = _storyEventSystem.CheckTrigger(triggerType, triggerValue, _flags);
        if (evt == null) return;

        _pendingStoryEvent = evt;

        // Load and play the story dialogue
        if (!string.IsNullOrEmpty(evt.DialogueFile) && File.Exists(DataPath.Resolve(evt.DialogueFile)))
        {
            _dialogueSystem.LoadFromFile(evt.DialogueFile);

            // Derive the start node ID from the event ID (convention: "{eventId}_1")
            string startId = $"{evt.Id}_1";
            _dialogueSystem.StartDialogue(startId);

            if (_dialogueSystem.IsActive)
            {
                _overlay = OverlayState.Dialogue;
                _dialogueBox.Show(_dialogueSystem);

                // Record for cutscene replay
                string displayName = CutsceneReplaySystem.GetDisplayName(evt.Id);
                _cutsceneReplay.RecordEvent(evt.Id, displayName);
            }
        }
        else
        {
            // No dialogue file — just apply effects immediately
            ApplyStoryEventEffects(evt);
            _pendingStoryEvent = null;
        }
    }

    private void OnDialogueEnded()
    {
        _overlay = OverlayState.None;

        if (_pendingStoryEvent != null)
        {
            ApplyStoryEventEffects(_pendingStoryEvent);
            var evt = _pendingStoryEvent;
            _pendingStoryEvent = null;

            // If the event triggers a battle, transition to BattleScreen
            if (!string.IsNullOrEmpty(evt.StartBattle))
            {
                _lastPlayerX = _player.X;
                _lastPlayerY = _player.Y;
                MoveToScreen<BattleScreen>(b =>
                    b.ReturnPlayerPosition = (_lastPlayerX, _lastPlayerY));
                return;
            }

            // Check for chained flag_set triggers
            foreach (var flag in evt.SetFlags)
                CheckStoryEvent("flag_set", flag);
        }
    }

    private void ApplyStoryEventEffects(StoryEvent evt)
    {
        // Set flags
        foreach (var flag in evt.SetFlags)
            _flags.Add(flag);

        // Mark event completed
        _storyEventSystem.CompleteEvent(evt.Id);

        // Recruit character
        if (!string.IsNullOrEmpty(evt.RecruitCharacter))
        {
            string charId = evt.RecruitCharacter;
            if (!_partyState.Roster.Contains(charId))
            {
                _partyState.Roster.Add(charId);
                Debug.WriteLine($"Recruited: {charId}");
            }
            if (_partyState.ActiveParty.Count < 4 && !_partyState.ActiveParty.Contains(charId))
                _partyState.ActiveParty.Add(charId);
        }

        // Recruit pet
        if (!string.IsNullOrEmpty(evt.RecruitPet))
            Debug.WriteLine($"Pet recruited: {evt.RecruitPet} (placeholder)");

        // Unlock map for fast travel
        if (!string.IsNullOrEmpty(evt.UnlockMap))
        {
            _fastTravel.MarkVisited(evt.UnlockMap);
            Debug.WriteLine($"Map unlocked: {evt.UnlockMap}");
        }

        Debug.WriteLine($"Story event completed: {evt.Id}");
    }

    private void OnCutsceneReplaySelected(string eventId)
    {
        // Find the event's dialogue file from the story events data
        var evt = _storyEventSystem.CheckTrigger("__replay__", "__none__", _flags);
        // We need to find the event by ID — reload from the data
        // For replay, load the dialogue file associated with the event
        string dialogueFile = $"Data/dialogue/story/{eventId}.json";

        // Dream sequences have different paths
        if (eventId.StartsWith("dream"))
            dialogueFile = $"Data/dialogue/dreams/{eventId}.json";

        if (File.Exists(DataPath.Resolve(dialogueFile)))
        {
            _dialogueSystem.LoadFromFile(dialogueFile);
            string startId = $"{eventId}_1";
            _dialogueSystem.StartDialogue(startId);

            if (_dialogueSystem.IsActive)
            {
                _overlay = OverlayState.Dialogue;
                _dialogueBox.Show(_dialogueSystem);
                // Don't set _pendingStoryEvent — this is a replay, no effects
            }
        }
    }

    private SaveData BuildSaveData() => new()
    {
        Party = _partyState,
        CurrentScreen = nameof(OverworldScreen),
        PlayerX = _player.X,
        PlayerY = _player.Y,
        CurrentMap = _currentMapId,
        CompletedQuests = [],
        DiscoveredRecipes = [],
        Flags = _flags.ToDictionary(f => f, _ => true),
        VisitedMaps = _fastTravel.VisitedMaps.ToList(),
        CompletedStoryEvents = _storyEventSystem.CompletedEvents.ToList(),
        ShownTutorials = _flags.Where(f => f.StartsWith("tutorial_")).ToList(),
        SaveTime = DateTime.Now,
        PlayTime = DateTime.Now - _sessionStart,
    };

    private void OnLoadRequested(SaveData data)
    {
        _partyState = data.Party;
        if (data.VisitedMaps.Count > 0)
            _fastTravel.LoadFrom(data.VisitedMaps);

        // Restore flags
        _flags.Clear();
        foreach (var (key, _) in data.Flags)
            _flags.Add(key);

        // Restore completed story events
        _storyEventSystem.RestoreCompleted(data.CompletedStoryEvents);

        _overlay = OverlayState.None;
    }
}
