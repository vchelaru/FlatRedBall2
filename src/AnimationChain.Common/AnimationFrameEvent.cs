namespace FlatRedBall2.Animation;

/// <summary>
/// A named event authored on an animation frame (e.g. <c>"Footstep"</c>, <c>"SpawnBubble"</c>).
/// Playback raises it when the frame is entered, so game code reacts by name instead of by frame
/// index, and inserting or reordering frames in the AnimationEditor moves the event with its frame.
/// </summary>
public class AnimationFrameEvent
{
    /// <summary>The event's name, matched by game code. Names need not be unique within a frame.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional free-form payload (a number, a target name, a JSON blob). The engine never parses it;
    /// interpreting it is up to the game. <c>null</c> when unset.
    /// </summary>
    public string? Data { get; set; }

    /// <summary>Returns a copy, so a frame copy never shares event instances with its source.</summary>
    public AnimationFrameEvent Clone() => new() { Name = Name, Data = Data };
}
