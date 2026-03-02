using FlatRedBall2;
using MonoGameGum.GueDeriving;

namespace SampleProject1.Entities;

public class ScoreFloater : Entity
{
    private float _lifetime;
    private const float Duration = 1.0f;

    private TextRuntime _label = null!;
    private int _points;

    public int Points
    {
        get => _points;
        set
        {
            _points = value;
            if (_label != null) _label.Text = $"+{value}";
        }
    }

    public override void CustomInitialize()
    {
        VelocityY = 60f;
        Drag = 1.5f;

        _label = new TextRuntime { Text = $"+{_points}", FontSize = 18 };
        AddGum(_label);
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime += time.DeltaSeconds;
        if (_lifetime >= Duration)
            Destroy();
    }
}
