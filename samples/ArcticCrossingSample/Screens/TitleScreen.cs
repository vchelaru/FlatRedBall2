using ArcticCrossingSample.Data;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Screens;

public class TitleScreen : Screen
{
    public GameState State { get; set; } = new();

    // Decorative elements
    private readonly List<AxisAlignedRectangle> _driftingIce = new();
    private readonly List<AxisAlignedRectangle> _snowflakes = new();

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 35, 70);

        SpawnDecorations();
        BuildMenu();
    }

    private void SpawnDecorations()
    {
        // Water band at bottom
        var water = new AxisAlignedRectangle
        {
            Width = 2000f,
            Height = 200f,
            IsVisible = true,
            IsFilled = true,
            Color = new XnaColor(20, 55, 120, 180),
            Y = -260f,
        };
        Add(water);

        // Drifting ice blocks
        for (int i = 0; i < 6; i++)
        {
            var ice = new AxisAlignedRectangle
            {
                Width = Engine.Random.Between(60f, 120f),
                Height = Engine.Random.Between(15f, 25f),
                IsVisible = true,
                IsFilled = true,
                Color = new XnaColor(200, 220, 240, Engine.Random.Between(150, 220)),
                X = Engine.Random.Between(-600f, 600f),
                Y = Engine.Random.Between(-220f, -170f),
            };
            Add(ice);
            _driftingIce.Add(ice);
        }

        // Mountain silhouette
        var mountain = new AxisAlignedRectangle
        {
            Width = 200f,
            Height = 300f,
            IsVisible = true,
            IsFilled = true,
            Color = new XnaColor(50, 60, 85, 160),
            X = 400f,
            Y = 0f,
        };
        Add(mountain);

        // Snowflakes
        for (int i = 0; i < 15; i++)
        {
            var flake = new AxisAlignedRectangle
            {
                Width = Engine.Random.Between(2f, 4f),
                Height = Engine.Random.Between(2f, 4f),
                IsVisible = true,
                IsFilled = true,
                Color = new XnaColor(255, 255, 255, Engine.Random.Between(60, 150)),
                X = Engine.Random.Between(-640f, 640f),
                Y = Engine.Random.Between(-360f, 360f),
            };
            Add(flake);
            _snowflakes.Add(flake);
        }
    }

    private void BuildMenu()
    {
        var menu = new StackPanel();
        menu.Spacing = 16;
        menu.Anchor(Anchor.Center);

        var title = new Label();
        title.Text = "ARCTIC CROSSING";
        menu.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = "Cross the Atlantic. Reach the Mountain.";
        menu.AddChild(subtitle);

        var startBtn = new Button();
        startBtn.Text = "New Game";
        startBtn.Click += (_, _) => MoveToScreen<CharacterSelectScreen>(s => s.State = State);
        menu.AddChild(startBtn);

        if (State.HighestUnlockedPhase > 1)
        {
            var continueBtn = new Button();
            continueBtn.Text = "Phase Select";
            continueBtn.Click += (_, _) => MoveToScreen<PhaseSelectScreen>(s => s.State = State);
            menu.AddChild(continueBtn);
        }

        Add(menu);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Animate drifting ice
        foreach (var ice in _driftingIce)
        {
            ice.X -= 15f * time.DeltaSeconds;
            if (ice.X < -700f) ice.X = 700f;
        }

        // Animate snowflakes
        foreach (var flake in _snowflakes)
        {
            flake.X -= 10f * time.DeltaSeconds;
            flake.Y -= 20f * time.DeltaSeconds;
            if (flake.X < -650f) flake.X = 650f;
            if (flake.Y < -370f) flake.Y = 370f;
        }
    }
}
