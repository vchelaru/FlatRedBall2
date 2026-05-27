using FlatRedBall.AnimationChain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AnimationChainSample;

/// <summary>
/// Minimal demo: loads FrbCon2026Icons.achx (22 named chains) and cycles through them.
///
/// Controls:
///   Space  — cycle to the next chain
///   R      — hot-reload FrbCon2026Icons.achx from disk (try editing frame timings while running)
///   Escape — exit
///
/// Drop frbcon-2026.png next to FrbCon2026Icons.achx in Content/ to see real art.
/// Until then the sprites draw as invisible (null texture) but chain cycling still works.
/// </summary>
public class Game1 : Game
{
    private static readonly string[] ChainOrder =
    [
        "FrbCon2026_Row01", "FrbCon2026_Row02", "FrbCon2026_Row03", "FrbCon2026_Row04",
        "FrbCon2026_Row05", "FrbCon2026_Row06", "FrbCon2026_Row07", "FrbCon2026_Row08",
        "FrbCon2026_Row09", "FrbCon2026_Row10", "FrbCon2026_Row11", "FrbCon2026_Row12",
        "FrbCon2026_Row13", "FrbCon2026_Row14", "FrbCon2026_Row15", "FrbCon2026_Row16",
        "FrbCon2026_Row17", "FrbCon2026_Row18", "FrbCon2026_Row19", "FrbCon2026_Row20",
        "FrbCon2026_Row21", "FrbCon2026_Row20Copy",
    ];

    private const string AchxPath = "Content/FrbCon2026Icons.achx";

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    private AchxLoader _loader = null!;
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

        // AchxLoader resolves frbcon-2026.png relative to the .achx file.
        // Missing PNG -> null textures -> DrawAnimation no-ops gracefully.
        _loader     = new AchxLoader(GraphicsDevice);
        _animations = _loader.Load(AchxPath);

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
            bool ok = _loader.TryReload(_animations, AchxPath);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing) _loader?.Dispose();
        base.Dispose(disposing);
    }

    private bool IsPressed(KeyboardState cur, Keys key) =>
        cur.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

    private string CurrentStatus() =>
        $"Chain: {_player.CurrentChain?.Name ?? "none"}  ({_player.CurrentChain?.Count ?? 0} frames)";

    private void UpdateTitle() =>
        Window.Title = $"AnimationChain.MonoGame -- {CurrentStatus()}   [Space] cycle  [R] reload  [Esc] quit";
}
