using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;

namespace Strikers1945Sample.Screens;

public class TitleScreen : Screen
{
    private const int StarCount = 60;
    private readonly AxisAlignedRectangle[] _stars = new AxisAlignedRectangle[StarCount];
    private readonly float[] _starSpeeds = new float[StarCount];

    // Cinematic planes (4 planes in formation)
    private readonly Sprite[] _planes = new Sprite[4];
    private readonly string[] _planeSprites = { "ship_0000", "ship_0001", "ship_0002", "ship_0003" };

    // UI elements that animate in
    private TextRuntime _titleText = null!;
    private TextRuntime _subtitleText = null!;
    private TextRuntime _pressText = null!;
    private TextRuntime _ctrlHeader = null!;
    private readonly TextRuntime[] _ctrlLines = new TextRuntime[4];
    private AxisAlignedRectangle _topLine = null!;
    private AxisAlignedRectangle _botLine = null!;

    // Splash screen elements
    private TextRuntime _splashTitle = null!;
    private TextRuntime _splashPrompt = null!;
    private AxisAlignedRectangle _splashLine1 = null!;
    private AxisAlignedRectangle _splashLine2 = null!;

    private float _timer;
    private float _blinkTimer;
    private float _splashBlinkTimer;
    private bool _splashDone;  // true once splash dismissed
    private bool _ready;       // true once cinematic is done and input is accepted

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(4, 5, 14);

        var rng = new Random();
        float halfW = Camera.TargetWidth / 2f;
        float halfH = Camera.TargetHeight / 2f;

        // === SPLASH SCREEN (shown first, before cinematic) ===
        _splashTitle = new TextRuntime
        {
            Text = "STRIKERS 1945",
            FontSize = 28,
            Red = 220, Green = 180, Blue = 60,
        };
        _splashTitle.Anchor(Anchor.TopLeft);
        _splashTitle.X = 95;
        _splashTitle.Y = 300;
        Add(_splashTitle);

        _splashPrompt = new TextRuntime
        {
            Text = "PRESS ANY KEY",
            FontSize = 18,
            Red = 200, Green = 200, Blue = 220,
        };
        _splashPrompt.Anchor(Anchor.TopLeft);
        _splashPrompt.X = 140;
        _splashPrompt.Y = 400;
        Add(_splashPrompt);

        _splashLine1 = new AxisAlignedRectangle
        {
            Width = 300f, Height = 1f, Color = new Color(120, 100, 40),
            Visible = true, IsFilled = true, X = 0f, Y = halfH - 290f, Z = -1f,
        };
        _splashLine2 = new AxisAlignedRectangle
        {
            Width = 300f, Height = 1f, Color = new Color(120, 100, 40),
            Visible = true, IsFilled = true, X = 0f, Y = halfH - 360f, Z = -1f,
        };
        Add(_splashLine1);
        Add(_splashLine2);

        // Starfield — varied sizes and brightness
        for (int i = 0; i < StarCount; i++)
        {
            float size = i < 8 ? 3f : i < 24 ? 2f : 1f;
            int b = i < 8 ? 255 : i < 24 ? 160 : 90;
            var star = new AxisAlignedRectangle
            {
                Width = size, Height = size,
                Color = new Color(b, b, (int)(b * 0.85f)),
                Visible = true, IsFilled = true,
                X = (float)(rng.NextDouble() * Camera.TargetWidth - halfW),
                Y = (float)(rng.NextDouble() * Camera.TargetHeight - halfH),
                Z = -5f,
            };
            _stars[i] = star;
            _starSpeeds[i] = (3.5f - size) * 20f + 10f + (float)rng.NextDouble() * 25f;
            Add(star);
        }

        // Load 4 planes — start off-screen below
        for (int i = 0; i < 4; i++)
        {
            var tex = Engine.ContentManager.Load<Texture2D>(_planeSprites[i]);
            _planes[i] = new Sprite
            {
                Texture = tex,
                TextureScale = 3.5f,
                X = 0f,
                Y = -halfH - 100f - i * 40f,
                Z = -2f,
                IsVisible = false, // hidden until cinematic starts
            };
            Add(_planes[i]);
        }

        // Gold decorative lines — start zero width
        _topLine = new AxisAlignedRectangle
        {
            Width = 0f, Height = 2f, Color = new Color(220, 180, 60),
            Visible = true, IsFilled = true, X = 0f, Y = halfH - 105f, Z = -1f,
        };
        _botLine = new AxisAlignedRectangle
        {
            Width = 0f, Height = 2f, Color = new Color(220, 180, 60),
            Visible = true, IsFilled = true, X = 0f, Y = halfH - 215f, Z = -1f,
        };
        Add(_topLine);
        Add(_botLine);

        // Title — starts off-screen above
        _titleText = new TextRuntime
        {
            Text = "STRIKERS 1945",
            FontSize = 36,
            Red = 255, Green = 220, Blue = 80,
        };
        _titleText.Anchor(Anchor.TopLeft);
        _titleText.X = 68;
        _titleText.Y = -60; // off-screen
        Add(_titleText);

        // Subtitle — starts invisible
        _subtitleText = new TextRuntime
        {
            Text = "A Vertical Shmup",
            FontSize = 18,
            Red = 0, Green = 0, Blue = 0, // invisible initially
        };
        _subtitleText.Anchor(Anchor.TopLeft);
        _subtitleText.X = 136;
        _subtitleText.Y = 172;
        Add(_subtitleText);

        // Press start — invisible until ready
        _pressText = new TextRuntime
        {
            Text = "PRESS Z OR SPACE",
            FontSize = 24,
            Red = 0, Green = 0, Blue = 0,
        };
        _pressText.Anchor(Anchor.TopLeft);
        _pressText.X = 100;
        _pressText.Y = 430;
        Add(_pressText);

        // Controls — invisible until ready
        _ctrlHeader = new TextRuntime { Text = "- CONTROLS -", FontSize = 16, Red = 0, Green = 0, Blue = 0 };
        _ctrlHeader.Anchor(Anchor.TopLeft); _ctrlHeader.X = 160; _ctrlHeader.Y = 510; Add(_ctrlHeader);

        string[] ctrlTexts = { "Arrows .... Move", "Z/Space ... Fire", "Hold Fire . Charge", "X ......... Bomb" };
        for (int i = 0; i < 4; i++)
        {
            _ctrlLines[i] = new TextRuntime { Text = ctrlTexts[i], FontSize = 14, Red = 0, Green = 0, Blue = 0 };
            _ctrlLines[i].Anchor(Anchor.TopLeft);
            _ctrlLines[i].X = 110;
            _ctrlLines[i].Y = 545 + i * 30;
            Add(_ctrlLines[i]);
        }
    }

    private void StartCinematic()
    {
        _splashDone = true;
        _timer = 0f;

        // Hide splash elements
        _splashTitle.Red = 0; _splashTitle.Green = 0; _splashTitle.Blue = 0;
        _splashPrompt.Red = 0; _splashPrompt.Green = 0; _splashPrompt.Blue = 0;
        _splashLine1.Visible = false;
        _splashLine2.Visible = false;

        // Show planes
        for (int i = 0; i < 4; i++)
            _planes[i].IsVisible = true;
    }

    public override void CustomActivity(FrameTime time)
    {
        float dt = time.DeltaSeconds;
        float halfW = Camera.TargetWidth / 2f;
        float halfH = Camera.TargetHeight / 2f;
        var kb = Engine.InputManager.Keyboard;

        // Starfield always scrolls
        for (int i = 0; i < StarCount; i++)
        {
            _stars[i].Y -= _starSpeeds[i] * dt;
            if (_stars[i].Y < -halfH - 4f)
            {
                _stars[i].Y = halfH + 4f;
                _stars[i].X = Engine.Random.Between(-halfW, halfW);
            }
        }

        // === SPLASH STATE ===
        if (!_splashDone)
        {
            _splashBlinkTimer += dt;
            if (_splashBlinkTimer >= 1.4f) _splashBlinkTimer -= 1.4f;
            bool bright = _splashBlinkTimer < 0.9f;
            _splashPrompt.Red = bright ? 200 : 60;
            _splashPrompt.Green = bright ? 200 : 60;
            _splashPrompt.Blue = bright ? 220 : 80;

            if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space) || kb.WasKeyPressed(Keys.Enter))
                StartCinematic();
            return;
        }

        // === CINEMATIC PHASES ===
        _timer += dt;

        // Phase 1 (0-2s): Planes fly up from below in diamond formation
        if (_timer < 2f)
        {
            float t = _timer / 2f; // 0→1
            float ease = t * t * (3f - 2f * t); // smoothstep
            // Diamond formation target: lead at center, two flanks, one trailing
            float baseY = Lerp(-halfH - 100f, 40f, ease);
            _planes[0].X = 0f;       _planes[0].Y = baseY;          // lead
            _planes[1].X = -70f;     _planes[1].Y = baseY - 40f;    // left wing
            _planes[2].X = 70f;      _planes[2].Y = baseY - 40f;    // right wing
            _planes[3].X = 0f;       _planes[3].Y = baseY - 80f;    // tail
        }
        // Phase 2 (2-3.5s): Planes break formation — fly to corners
        else if (_timer < 3.5f)
        {
            float t = (_timer - 2f) / 1.5f;
            float ease = t * t * (3f - 2f * t);
            _planes[0].X = Lerp(0f, -130f, ease);   _planes[0].Y = Lerp(40f, 160f, ease);
            _planes[1].X = Lerp(-70f, 130f, ease);   _planes[1].Y = Lerp(0f, 160f, ease);
            _planes[2].X = Lerp(70f, -130f, ease);   _planes[2].Y = Lerp(0f, -80f, ease);
            _planes[3].X = Lerp(0f, 130f, ease);     _planes[3].Y = Lerp(-40f, -80f, ease);
        }
        // Phase 3 (3.5s+): Planes orbit slowly
        else
        {
            float orbitT = _timer - 3.5f;
            float orbitSpeed = 0.4f;
            float r1 = 150f, r2 = 130f;
            for (int i = 0; i < 4; i++)
            {
                float angle = orbitT * orbitSpeed + i * MathF.PI / 2f;
                float rx = (i % 2 == 0) ? r1 : r2;
                float ry = rx * 0.5f;
                _planes[i].X = MathF.Cos(angle) * rx;
                _planes[i].Y = 40f + MathF.Sin(angle) * ry;
            }
        }

        // Phase 2 (2-3s): Title slams down from above
        if (_timer > 2f)
        {
            float t = Math.Clamp((_timer - 2f) / 0.6f, 0f, 1f);
            // Overshoot ease for slam effect
            float ease = t < 1f ? 1f - MathF.Pow(1f - t, 3f) * MathF.Cos(t * MathF.PI * 1.5f) : 1f;
            _titleText.Y = Lerp(-60f, 125f, Math.Clamp(ease, 0f, 1.1f));
        }

        // Phase 3 (3s): Gold lines expand
        if (_timer > 3f)
        {
            float t = Math.Clamp((_timer - 3f) / 0.8f, 0f, 1f);
            float w = Lerp(0f, 380f, t);
            _topLine.Width = w;
            _botLine.Width = w;
        }

        // Phase 4 (3.5s): Subtitle fades in
        if (_timer > 3.5f)
        {
            float t = Math.Clamp((_timer - 3.5f) / 1f, 0f, 1f);
            int r = (int)(180 * t), g = (int)(180 * t), b2 = (int)(210 * t);
            _subtitleText.Red = r; _subtitleText.Green = g; _subtitleText.Blue = b2;
        }

        // Phase 5 (5s): Press start + controls fade in
        if (_timer > 5f)
        {
            _ready = true;
            float t = Math.Clamp((_timer - 5f) / 1f, 0f, 1f);

            // Pulsing press text
            _blinkTimer += dt;
            if (_blinkTimer >= 1.2f) _blinkTimer -= 1.2f;
            bool bright = _blinkTimer < 0.8f;
            int pr = bright ? 255 : 60, pg = bright ? 255 : 60, pb = bright ? 255 : 60;
            _pressText.Red = (int)(pr * t); _pressText.Green = (int)(pg * t); _pressText.Blue = (int)(pb * t);

            // Controls header (gold)
            _ctrlHeader.Red = (int)(200 * t); _ctrlHeader.Green = (int)(160 * t); _ctrlHeader.Blue = (int)(60 * t);

            // Control lines (staggered fade)
            for (int i = 0; i < 4; i++)
            {
                float ct = Math.Clamp((_timer - 5.2f - i * 0.15f) / 0.6f, 0f, 1f);
                _ctrlLines[i].Red = (int)(150 * ct);
                _ctrlLines[i].Green = (int)(150 * ct);
                _ctrlLines[i].Blue = (int)(180 * ct);
            }
        }

        // Skip cinematic on any key
        if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space) || kb.WasKeyPressed(Keys.Enter))
        {
            if (_ready)
                MoveToScreen<PlaneSelectScreen>();
            else
                _timer = 5f; // skip to ready state
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public override void CustomDestroy() { }
}
