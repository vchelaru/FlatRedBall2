using FlatRedBall2;
using FlatRedBall2.Collision;

namespace Beefball.Entities;

/// <summary>Invisible solid rectangle. The visible neon walls are drawn by <see cref="ArenaVisuals"/>.</summary>
public class Wall : Entity
{
    public float Width { get; set; } = 10f;
    public float Height { get; set; } = 10f;

    public override void CustomInitialize()
    {
        Add(new AARect { Width = Width, Height = Height });
    }
}

/// <summary>Plugs a goal mouth for players only. The puck passes through because it has no relationship with it.</summary>
public class GoalBlocker : Wall
{
}
