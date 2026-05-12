using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// End-to-end data-model tests that mirror the documented AnimationEditor tutorial workflow:
/// https://docs.flatredball.com/flatredball/glue-gluevault-component-pages-animationeditor-plugin
///
/// Each test corresponds to a specific step in the docs so that any regression in the
/// documented user flow is immediately visible.
/// </summary>
[Collection("SequentialSingletons")]
public class WorkflowTutorialTests
{
    // ── Step 1: Create "Idle" animation ──────────────────────────────────────
    // Docs: "Click the + icon … Enter the name 'Idle' and click OK."

    [Fact]
    public async Task Step1_CreateIdleChain_RenameToIdle_ChainNameIsIdle()
    {
        var acls = TestHelpers.SetupFreshAcls();

        await AppCommands.Self.AddAnimationChain();
        var chain = SelectedState.Self.SelectedChain!;
        bool renamed = AppCommands.Self.RenameChain(chain, "Idle");

        Assert.True(renamed, "RenameChain should succeed for a unique name");
        Assert.Equal("Idle", chain.Name);
        Assert.Single(acls.AnimationChains);
    }

    // ── Step 2: Add frame (initially untextured) ─────────────────────────────
    // Docs: "Right-click on the newly-added Idle animation → Select 'Add Frame'."

    [Fact]
    public void Step2_AddFrameToIdleChain_FrameStartsWithNoTexture()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Idle");

        AppCommands.Self.AddFrame(chain);

        Assert.Single(chain.Frames);
        Assert.Equal(string.Empty, chain.Frames[0].TextureName);
    }

    // ── Step 3: Assign texture ────────────────────────────────────────────────
    // Docs: "Click the button for the TextureName property … Navigate to where you saved the file."

    [Fact]
    public void Step3_SetTextureName_FrameHasTextureName()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Idle");
        AppCommands.Self.AddFrame(chain);
        var frame = chain.Frames[0];

        AppCommands.Self.SetFrameTextureName(frame, "Idle.png");

        Assert.Equal("Idle.png", frame.TextureName);
    }

    // ── Step 4: SpriteSheet cell-size calculation ─────────────────────────────
    // Docs: "Set the cell height to '2 cells'. The plugin automatically calculates
    //         this value as 64 pixels." (texture is 128 px tall)
    // Docs: "Set the cell width to '4 cells'."  (texture is 128 px wide → 32 px)

    [Fact]
    public void Step4_SpriteSheet_2CellHeight_On128pxTexture_CellHeightIs64px()
    {
        int cellHeight = TileCoordinateCalculator.CellSizeFromCount(2, 128);
        Assert.Equal(64, cellHeight);
    }

    [Fact]
    public void Step4_SpriteSheet_4CellWidth_On128pxTexture_CellWidthIs32px()
    {
        int cellWidth = TileCoordinateCalculator.CellSizeFromCount(4, 128);
        Assert.Equal(32, cellWidth);
    }

    // ── Step 5: Eight frames — each mapped to a different spritesheet cell ────
    // Docs: "Try adding more frames so that your animation includes all 8 idle frames."
    // Grid layout: 4 columns × 2 rows, each cell = 32 × 64 px on a 128 × 128 texture.

    [Fact]
    public void Step5_IdleAnimation_8SpriteSheetFrames_AllHaveUniqueUVRects()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Idle");

        int cellW = 32, cellH = 64, texW = 128, texH = 128;
        // 4 columns × 2 rows = 8 cells
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                var (l, r) = TileCoordinateCalculator.GetLeftRight(col, cellW, texW);
                var (t, b) = TileCoordinateCalculator.GetTopBottom(row, cellH, texH);
                chain.Frames.Add(new AnimationFrameSave
                {
                    TextureName      = "Idle.png",
                    LeftCoordinate   = l, RightCoordinate  = r,
                    TopCoordinate    = t, BottomCoordinate = b,
                    FrameLength      = 0.1f,
                    ShapeCollectionSave = new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave()
                });
            }
        }

        Assert.Equal(8, chain.Frames.Count);

        // All 8 UV rects must be unique (no two frames cover the same cell)
        var uvKeys = chain.Frames
            .Select(f => $"{f.LeftCoordinate:F4},{f.TopCoordinate:F4},{f.RightCoordinate:F4},{f.BottomCoordinate:F4}")
            .ToList();
        Assert.Equal(8, uvKeys.Distinct().Count());
    }

    [Fact]
    public void Step5_IdleAnimation_Frame0_TopLeftCell_CorrectUV()
    {
        // Cell (col=0, row=0): left=0, right=32/128=0.25, top=0, bottom=64/128=0.5
        var (left,  right)  = TileCoordinateCalculator.GetLeftRight(0, 32, 128);
        var (top,   bottom) = TileCoordinateCalculator.GetTopBottom(0, 64, 128);

        Assert.Equal(0f,        left,   precision: 5);
        Assert.Equal(0.25f,     right,  precision: 5);
        Assert.Equal(0f,        top,    precision: 5);
        Assert.Equal(0.5f,      bottom, precision: 5);
    }

    [Fact]
    public void Step5_IdleAnimation_Frame7_BottomRightCell_CorrectUV()
    {
        // Cell (col=3, row=1): left=96/128=0.75, right=1.0, top=64/128=0.5, bottom=1.0
        var (left,  right)  = TileCoordinateCalculator.GetLeftRight(3, 32, 128);
        var (top,   bottom) = TileCoordinateCalculator.GetTopBottom(1, 64, 128);

        Assert.Equal(0.75f,     left,   precision: 5);
        Assert.Equal(1f,        right,  precision: 5);
        Assert.Equal(0.5f,      top,    precision: 5);
        Assert.Equal(1f,        bottom, precision: 5);
    }

    // ── Step 6: All 8 frames carry the same texture and default duration ────────
    // Docs: "the plugin remembers the cell settings, additional frames can be added
    //         with just a few mouse clicks … view the animation … clicking on the animation"

    [Fact]
    public void Step6_IdleAnimation_AllEightFrames_HaveSameTextureAndDefaultDuration()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Idle");

        int cellW = 32, cellH = 64, texW = 128, texH = 128;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                var (l, r) = TileCoordinateCalculator.GetLeftRight(col, cellW, texW);
                var (t, b) = TileCoordinateCalculator.GetTopBottom(row, cellH, texH);
                var frame = new AnimationFrameSave
                {
                    TextureName      = "Idle.png",
                    LeftCoordinate   = l, RightCoordinate  = r,
                    TopCoordinate    = t, BottomCoordinate = b,
                    FrameLength      = 0.1f,
                    ShapeCollectionSave = new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave()
                };
                chain.Frames.Add(frame);
            }

        Assert.All(chain.Frames, f => Assert.Equal("Idle.png", f.TextureName));
        Assert.All(chain.Frames, f => Assert.Equal(0.1f, f.FrameLength, precision: 5));
    }

    [Fact]
    public void Step6_SetAllFrameLengths_AllFramesUpdatedToNewDuration()
    {
        var acls  = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Idle", frameCount: 8);

        AppCommands.Self.SetAllFrameLengths(chain, 0.05f);

        Assert.All(chain.Frames, f => Assert.Equal(0.05f, f.FrameLength, precision: 5));
    }

    // ── Step 7: Add "Run" animation (Pixel coordinate mode) ───────────────────
    // Docs: "Add Animation … Enter the name 'Run' … Add Frame … change 'Sprite Sheet' to 'Pixel'."

    [Fact]
    public async Task Step7_CreateRunAnimation_AclsNowHasTwoChains()
    {
        var acls = TestHelpers.SetupFreshAcls();

        await AppCommands.Self.AddAnimationChain();
        var idle = SelectedState.Self.SelectedChain!;
        AppCommands.Self.RenameChain(idle, "Idle");

        await AppCommands.Self.AddAnimationChain();
        var run = SelectedState.Self.SelectedChain!;
        AppCommands.Self.RenameChain(run, "Run");

        Assert.Equal(2, acls.AnimationChains.Count);
        Assert.Equal("Idle", acls.AnimationChains[0].Name);
        Assert.Equal("Run",  acls.AnimationChains[1].Name);
    }

    // ── Step 8: Pixel mode — define frame by dragging handles ─────────────────
    // Docs: "You will now see a white square with 8 circle handles. You can push on the
    //         circles and drag to resize the frame." → AddFrameFromPixelBounds simulates this.

    [Fact]
    public void Step8_PixelMode_AddFrameFromBounds_UVsMatchPixelRegion()
    {
        var acls = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(acls, "Run");

        // Doc example: first run frame covers roughly the left half of a 128×128 sprite sheet
        AppCommands.Self.AddFrameFromPixelBounds(chain, "Running.png", 0, 0, 64, 128, 128, 128);

        var frame = chain.Frames[0];
        Assert.Equal(0f,   frame.LeftCoordinate,   precision: 5);
        Assert.Equal(0.5f, frame.RightCoordinate,  precision: 5);
        Assert.Equal(0f,   frame.TopCoordinate,    precision: 5);
        Assert.Equal(1f,   frame.BottomCoordinate, precision: 5);
    }

    // ── Step 9: Shift frames using RelativeY ──────────────────────────────────
    // Docs: "Change the RelativeY value so that the character is positioned properly
    //         relative to the guide … the preview window will update immediately."

    [Fact]
    public void Step9_RelativeY_NegativeValue_StoresCorrectly()
    {
        // A negative RelativeY means the sprite moves down in game space.
        // Setting it explicitly and reading it back must round-trip exactly.
        var frame = new AnimationFrameSave();
        frame.RelativeY = -20f;
        Assert.Equal(-20f, frame.RelativeY, precision: 5);
    }

    [Fact]
    public void Step9_RelativeY_PositiveValue_StoresCorrectly()
    {
        // Positive RelativeY moves the sprite upward (FlatRedBall +Y = up).
        var frame = new AnimationFrameSave();
        frame.RelativeY = 8f;
        Assert.Equal(8f, frame.RelativeY, precision: 5);
    }

    [Fact]
    public void Step9_RelativeX_PositiveValue_StoresCorrectly()
    {
        var frame = new AnimationFrameSave();
        frame.RelativeX = 12f;
        Assert.Equal(12f, frame.RelativeX, precision: 5);
    }
}
