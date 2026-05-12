# Platformer Animation Template

A ready-made `.achx` and spritesheet live in `.claude/templates/AnimationChains/`. When a game needs character animations, copy these two files into the game's content directory:

1. `PlatformerAnimations.achx` → rename to match the character (e.g. `Player.achx`)
2. `AnimatedSpritesheet.png` → copy alongside the `.achx`

The `.achx` references the `.png` by relative path (`FileRelativeTextures` is `true`), so they must be in the same directory. Add a `.csproj` include to copy them to output:

```xml
<ItemGroup>
  <Content Include="Content/Animations/**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## What the template provides

48 animation chains for a 16x32 character on a shared spritesheet. Includes idle, walk, run, jump, fall, duck, kick, slide, skid, climb, wall-slide, swim, look-up, victory, and shoot variants (left/right pairs). Also includes non-character chains: Coin, Block, InteractiveBlock, Fireball, Shot, BouncyPlatform, and particle effects.

## Customizing

Delete chains you don't need, rename chains to match your game's conventions, and adjust `FrameLength` for timing. If using a different spritesheet, update `TextureName` and frame coordinates in each `<Frame>`. See `achx-authoring.md` for the XML schema.
