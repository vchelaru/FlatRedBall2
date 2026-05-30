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
///   R      -- reload hero.achx from the content stream
///   Escape -- exit
/// </summary>
public class Game1 : Game
{
    private const string AchxPath = "Content/hero.achx";

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    // Real spritesheet loaded from AnimatedSpritesheet.png
    private Texture2D _spriteSheet = null!;

    private AnimationChainList _animations = null!;
    private AnimationPlayer _player = null!;
    private string[] _chainOrder = null!;
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

        _animations = LoadAnimations();

        // Build the chain order from all available animations
        _chainOrder = _animations.Select(ac => ac.Name).ToArray();

        if (_chainOrder.Length > 0)
        {
            _player = new AnimationPlayer(_animations);
            _player.Play(_chainOrder[_chainIndex]);
        }

        UpdateTitle();
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        if (keys.IsKeyDown(Keys.Escape))
            Exit();

        if (IsPressed(keys, Keys.Space))
        {
            _chainIndex = (_chainIndex + 1) % _chainOrder.Length;
            _player.Play(_chainOrder[_chainIndex]);
        }

        if (IsPressed(keys, Keys.R))
        {
            bool ok = TryReloadAnimations();
            Window.Title = ok
                ? $"Reloaded! -- {CurrentStatus()}"
                : "Reload failed -- check Content/hero.achx and try again";
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
        
        // Center the animation with a slight vertical offset to account for frame content
        // Most frames are 16x32 pixels, which when scaled 8x = 128x256 pixels
        const float scale = 8f;
        const float frameWidth = 16f;  // Most frames are 16 pixels wide
        const float frameHeight = 32f; // Most frames are 32 pixels tall
        float scaledWidth = frameWidth * scale;   // 128
        float scaledHeight = frameHeight * scale; // 256
        
        Vector2 center = new Vector2(GraphicsDevice.Viewport.Width / 2f - scaledWidth / 2f, 
                                     GraphicsDevice.Viewport.Height / 2f - scaledHeight / 2f);
        _spriteBatch.DrawAnimation(_player, center, Color.White, scale: scale);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private bool IsPressed(KeyboardState cur, Keys key) =>
        cur.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

    private AnimationChainList LoadAnimations()
    {
        using var achxStream = TitleContainer.OpenStream(AchxPath);
        var save = AnimationChainListSave.FromStream(achxStream);
        return save.ToAnimationChainList(_ => _spriteSheet);
    }

    private bool TryReloadAnimations()
    {
        try
        {
            using var achxStream = TitleContainer.OpenStream(AchxPath);
            return _animations.TryReloadFrom(achxStream, _ => _spriteSheet);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string CurrentStatus() =>
        $"Chain: {_player.CurrentChain?.Name ?? "none"}  ({_player.CurrentChain?.Count ?? 0} frames)";

    private void UpdateTitle() =>
        Window.Title = $"AnimationChain.MonoGame -- {CurrentStatus()}   [Space] cycle  [R] reload  [Esc] quit";
}
