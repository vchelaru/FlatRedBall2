using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AnimationChainCommonSample;

/// <summary>
/// Simplest possible use of <c>FlatRedBall.AnimationChain.Common</c>: parse the raw .achj data
/// (<see cref="AnimationChainListSave"/>), track elapsed seconds by hand, and pick/draw the current
/// frame directly with SpriteBatch. No AnimationChain/AnimationChainList/AnimationPlayer runtime
/// types involved -- just the on-disk data model.
///
/// Controls:
///   Space  -- toggle Walk / Idle
///   Escape -- exit
/// </summary>
public class Game1 : Game
{
    private const string AchjPath = "Content/hero.achj";

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _spriteSheet = null!;
    private KeyboardState _prevKeys;

    private AnimationChainListSave _save = null!;
    private AnimationChainSave _currentChain = null!;
    private double _secondsIntoAnimation;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _spriteSheet = Content.Load<Texture2D>("AnimatedSpritesheet");

        using var stream = TitleContainer.OpenStream(AchjPath);
        _save = AnimationChainListSave.FromStream(stream);

        _currentChain = GetChain("Walk");
    }

    private AnimationChainSave GetChain(string name)
    {
        foreach (var chain in _save.AnimationChains)
            if (chain.Name == name)
                return chain;
        throw new InvalidOperationException($"No chain named '{name}' in {AchjPath}");
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();

        if (keys.IsKeyDown(Keys.Escape))
            Exit();

        if (keys.IsKeyDown(Keys.Space) && !_prevKeys.IsKeyDown(Keys.Space))
        {
            _currentChain = GetChain(_currentChain.Name == "Walk" ? "Idle" : "Walk");
            _secondsIntoAnimation = 0;
        }

        _secondsIntoAnimation += gameTime.ElapsedGameTime.TotalSeconds;

        _prevKeys = keys;
        base.Update(gameTime);
    }

    // Walks the chain's frames, wrapping at the total length, to find which frame covers
    // _secondsIntoAnimation. This is exactly what AnimationPlayer does internally -- inlined here
    // since the point of this sample is showing that math, not hiding it behind a runtime type.
    private AnimationFrameSave GetCurrentFrame()
    {
        double totalLength = 0;
        foreach (var frame in _currentChain.Frames)
            totalLength += frame.FrameLength;

        double t = totalLength > 0 ? _secondsIntoAnimation % totalLength : 0;

        foreach (var frame in _currentChain.Frames)
        {
            if (t < frame.FrameLength)
                return frame;
            t -= frame.FrameLength;
        }

        return _currentChain.Frames[^1];
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 30, 40));

        var frame = GetCurrentFrame();
        var source = new Rectangle(
            (int)frame.LeftCoordinate,
            (int)frame.TopCoordinate,
            (int)(frame.RightCoordinate - frame.LeftCoordinate),
            (int)(frame.BottomCoordinate - frame.TopCoordinate));

        const float scale = 8f;
        var position = new Vector2(
            GraphicsDevice.Viewport.Width / 2f - source.Width * scale / 2f,
            GraphicsDevice.Viewport.Height / 2f - source.Height * scale / 2f);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(
            _spriteSheet,
            position,
            source,
            Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: scale,
            effects: frame.FlipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            layerDepth: 0f);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
