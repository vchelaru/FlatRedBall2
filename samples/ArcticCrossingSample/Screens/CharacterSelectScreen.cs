using ArcticCrossingSample.Data;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Screens;

public class CharacterSelectScreen : Screen
{
    public GameState State { get; set; } = new();

    private bool _isFemale;

    // Character preview shapes
    private AxisAlignedRectangle _previewHead = null!;
    private AxisAlignedRectangle _previewBody = null!;
    private AxisAlignedRectangle _previewLeftArm = null!;
    private AxisAlignedRectangle _previewRightArm = null!;
    private AxisAlignedRectangle _previewLeftLeg = null!;
    private AxisAlignedRectangle _previewRightLeg = null!;
    private Label _genderLabel = null!;

    // Male colors
    private static readonly XnaColor MaleHead = new(255, 160, 60, 255);
    private static readonly XnaColor MaleBody = new(220, 60, 50, 255);
    private static readonly XnaColor MaleArm = new(200, 50, 40, 255);
    private static readonly XnaColor MaleLeg = new(40, 50, 120, 255);

    // Female colors
    private static readonly XnaColor FemaleHead = new(255, 180, 200, 255);
    private static readonly XnaColor FemaleBody = new(160, 60, 180, 255);
    private static readonly XnaColor FemaleArm = new(140, 50, 160, 255);
    private static readonly XnaColor FemaleLeg = new(40, 130, 130, 255);

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 65);

        BuildPreview();
        BuildUi();
        UpdatePreview();
    }

    private void BuildPreview()
    {
        float cx = 0f, cy = 30f;

        _previewHead = new AxisAlignedRectangle
        {
            Width = 30f, Height = 30f, IsVisible = true, IsFilled = true,
            X = cx, Y = cy + 55f,
        };
        Add(_previewHead);

        _previewBody = new AxisAlignedRectangle
        {
            Width = 40f, Height = 50f, IsVisible = true, IsFilled = true,
            X = cx, Y = cy + 15f,
        };
        Add(_previewBody);

        _previewLeftArm = new AxisAlignedRectangle
        {
            Width = 10f, Height = 40f, IsVisible = true, IsFilled = true,
            X = cx - 25f, Y = cy + 10f,
        };
        Add(_previewLeftArm);

        _previewRightArm = new AxisAlignedRectangle
        {
            Width = 10f, Height = 40f, IsVisible = true, IsFilled = true,
            X = cx + 25f, Y = cy + 10f,
        };
        Add(_previewRightArm);

        _previewLeftLeg = new AxisAlignedRectangle
        {
            Width = 14f, Height = 34f, IsVisible = true, IsFilled = true,
            X = cx - 10f, Y = cy - 27f,
        };
        Add(_previewLeftLeg);

        _previewRightLeg = new AxisAlignedRectangle
        {
            Width = 14f, Height = 34f, IsVisible = true, IsFilled = true,
            X = cx + 10f, Y = cy - 27f,
        };
        Add(_previewRightLeg);
    }

    private void BuildUi()
    {
        var title = new Label();
        title.Text = "Choose Your Character";
        title.Anchor(Anchor.Top);
        title.Y = 30;
        Add(title);

        _genderLabel = new Label();
        _genderLabel.Anchor(Anchor.Center);
        _genderLabel.Y = 100;
        Add(_genderLabel);

        var instructions = new Label();
        instructions.Text = "Left/Right to switch, Space to confirm";
        instructions.Anchor(Anchor.BottomLeft);
        instructions.X = 20;
        instructions.Y = -30;
        Add(instructions);
    }

    private void UpdatePreview()
    {
        if (_isFemale)
        {
            _previewHead.Color = FemaleHead;
            _previewBody.Color = FemaleBody;
            _previewLeftArm.Color = FemaleArm;
            _previewRightArm.Color = FemaleArm;
            _previewLeftLeg.Color = FemaleLeg;
            _previewRightLeg.Color = FemaleLeg;
            _genderLabel.Text = "< Female >";
        }
        else
        {
            _previewHead.Color = MaleHead;
            _previewBody.Color = MaleBody;
            _previewLeftArm.Color = MaleArm;
            _previewRightArm.Color = MaleArm;
            _previewLeftLeg.Color = MaleLeg;
            _previewRightLeg.Color = MaleLeg;
            _genderLabel.Text = "< Male >";
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.Input.Keyboard;

        if (kb.WasKeyPressed(Keys.Left) || kb.WasKeyPressed(Keys.Right) ||
            kb.WasKeyPressed(Keys.A) || kb.WasKeyPressed(Keys.D))
        {
            _isFemale = !_isFemale;
            UpdatePreview();
        }

        if (kb.WasKeyPressed(Keys.Space) || kb.WasKeyPressed(Keys.Enter))
        {
            State.IsFemale = _isFemale;
            MoveToScreen<GameplayScreen>(s =>
            {
                s.PhaseIndex = 1;
                s.State = State;
            });
        }

        if (kb.WasKeyPressed(Keys.Escape))
        {
            MoveToScreen<TitleScreen>(s => s.State = State);
        }
    }
}
