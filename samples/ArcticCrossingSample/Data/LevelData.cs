using ArcticCrossingSample.Entities;
using Microsoft.Xna.Framework;

namespace ArcticCrossingSample.Data;

/// Describes one platform placement in a level.
public record PlatformPlacement(
    float X, float Y,
    float Width = 100f, float Height = 20f,
    PlatformType Type = PlatformType.Static,
    float MoveRangeX = 0f, float MoveRangeY = 0f, float MoveSpeed = 60f,
    float CrumbleDelay = 2f,
    float OneShotDelay = 0.5f);

/// Describes an NPC placement.
public record NpcPlacement(NpcKind Kind, float X, float Y, string HintText = "", float WaddleRange = 60f, bool CanBellySlide = false);

public enum NpcKind { PolarBear, Penguin, Seal, Unicorn }

/// Describes a checkpoint placement.
public record CheckpointPlacement(float X, float Y, int Index);

/// Describes a collectible placement.
public record CollectiblePlacement(float X, float Y, int PointValue = 100);

/// All data needed to build one phase/level.
public record LevelData(
    string PhaseName,
    int PhaseIndex,
    Color BackgroundColor,
    float PlayerStartX,
    float PlayerStartY,
    float DeathZoneY,
    float LevelLeftBound,
    float LevelRightBound,
    float LevelTopBound,
    PlatformPlacement[] Platforms,
    CheckpointPlacement[] Checkpoints,
    NpcPlacement[] Npcs,
    CollectiblePlacement[] Collectibles);
