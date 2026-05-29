using FlatRedBall.AnimationChain;
using FlatRedBall.AnimationChain.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AnimationChainSample;

/// <summary>
/// Demo: loads real FRB Guy animations from hero.achx and displays them on screen.
/// Uses the red character from AnimatedSpritesheet.png.
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

    // Real spritesheet loaded from AnimatedSpritesheet.png
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

        // Load the real spritesheet PNG
        _spriteSheet = Content.Load<Texture2D>("AnimatedSpritesheet");

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
}
