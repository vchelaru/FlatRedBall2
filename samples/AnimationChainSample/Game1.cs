using FlatRedBall.AnimationChain;
using FlatRedBall.AnimationChain.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AnimationChainSample;

/// <summary>
/// Minimal demo: loads hero.achx (Walk / Run / Idle) and plays each chain
/// on a procedural spritesheet -- no external art required.
///
/// Controls:
///   Space  -- cycle Walk -> Run -> Idle -> Walk
///   R      -- hot-reload hero.achx from disk (try editing frame timings while running)
///   Escape -- exit
/// </summary>
public class Game1 : Game
{
    private static readonly string[] ChainOrder = ["Walk", "Run", "Idle"];

    private const string AchxPath = "Content/hero.achx";

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    // Procedural 320x32 spritesheet: 10 frames at 32x32 each.
    // col 0-3 = Walk (reds), col 4-7 = Run (blues), col 8-9 = Idle (grays).
    private Texture2D _spriteSheet = null!;

    private AnimationChainList _animations = null!;
    private AnimationPlayer _player = null!;
    private int _chainIndex;

    private KeyboardState _prevKeys;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth  = 800,
            PreferredBackBufferHeight = 600,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _spriteSheet = CreateSpriteSheet(GraphicsDevice);

        var save = AnimationChainListSave.FromFile(AchxPath);
        _animations = save.ToAnimationChainList(_ => _spriteSheet);

        _player = new AnimationPlayer(_animations);
        _player.Play(ChainOrder[_chainIndex]);

        UpdateTitle();
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        if (keys.IsKeyDown(Keys.Escape))
            Exit();

        if (IsPressed(keys, Keys.Space))
        {
            _chainIndex = (_chainIndex + 1) % ChainOrder.Length;
            _player.Play(ChainOrder[_chainIndex]);
        }

        if (IsPressed(keys, Keys.R))
        {
            bool ok = _animations.TryReloadFrom(AchxPath, _ => _spriteSheet);
            Window.Title = ok
                ? $"Reloaded! -- {CurrentStatus()}"
                : "Reload failed (file busy?) -- try again";
        }

        _player.Update(gameTime.ElapsedGameTime);
        UpdateTitle();

        _prevKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 30, 40));

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.DrawAnimation(_player, new Vector2(400, 300), Color.White, scale: 8f);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private bool IsPressed(KeyboardState cur, Keys key) =>
        cur.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

    private string CurrentStatus() =>
        $"Chain: {_player.CurrentChain?.Name ?? "none"}  ({_player.CurrentChain?.Count ?? 0} frames)";

    private void UpdateTitle() =>
        Window.Title = $"AnimationChain.MonoGame -- {CurrentStatus()}   [Space] cycle  [R] reload  [Esc] quit";

    private static Texture2D CreateSpriteSheet(GraphicsDevice gd)
    {
        Color[] palette =
        [
            new Color(200,  50,  50),   // Walk 0
            new Color(230, 100,  50),   // Walk 1
            new Color(230, 100,  50),   // Walk 2
            new Color(200,  50,  50),   // Walk 3
            new Color( 50,  80, 220),   // Run 0
            new Color( 50, 160, 240),   // Run 1
            new Color( 80, 210, 255),   // Run 2
            new Color( 50, 160, 240),   // Run 3
            new Color(160, 160, 160),   // Idle 0
            new Color(210, 210, 210),   // Idle 1
        ];

        const int frameSize  = 32;
        const int frameCount = 10;
        int w = frameSize * frameCount;
        int h = frameSize;

        var texture = new Texture2D(gd, w, h);
        var data    = new Color[w * h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int  frame    = x / frameSize;
            int  lx       = x % frameSize;
            bool isBorder = lx == 0 || lx == frameSize - 1 || y == 0 || y == frameSize - 1;
            data[y * w + x] = isBorder ? Color.Black : palette[frame];
        }

        texture.SetData(data);
        return texture;
    }
}
