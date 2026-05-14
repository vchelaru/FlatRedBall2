using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
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
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        await ctx.AppCommands.AddAnimationChain();
        var chain = ctx.SelectedState.SelectedChain!;
        bool renamed = ctx.AppCommands.RenameChain(chain, "Idle");

        Assert.True(renamed, "RenameChain should succeed for a unique name");
        Assert.Equal("Idle", chain.Name);
        Assert.Single(acls.AnimationChains);
    }

    // ── Step 2: Add frame (initially untextured) ─────────────────────────────
    // Docs: "Right-click on the newly-added Idle animation → Select 'Add Frame'."

    [Fact]
    public void Step2_AddFrameToIdleChain_FrameStartsWithNoTexture()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle");

        ctx.AppCommands.AddFrame(chain);

        Assert.Single(chain.Frames);
        Assert.Equal(string.Empty, chain.Frames[0].TextureName);
    }

    // ── Step 3: Assign texture ────────────────────────────────────────────────
    // Docs: "Click the button for the TextureName property … Navigate to where you saved the file."

    [Fact]
    public void Step3_SetTextureName_FrameHasTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle");
        ctx.AppCommands.AddFrame(chain);
        var frame = chain.Frames[0];

        ctx.AppCommands.SetFrameTextureName(frame, "Idle.png");

        Assert.Equal("Idle.png", frame.TextureName);
    }

    // ── Step 6: Update all frame durations ────────────────────────────────────

    [Fact]
    public void Step6_SetAllFrameLengths_AllFramesUpdatedToNewDuration()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Idle", frameCount: 8);

        ctx.AppCommands.SetAllFrameLengths(chain, 0.05f);

        Assert.All(chain.Frames, f => Assert.Equal(0.05f, f.FrameLength, precision: 5));
    }

    // ── Step 7: Add "Run" animation (Pixel coordinate mode) ───────────────────
    // Docs: "Add Animation … Enter the name 'Run' … Add Frame … change 'Sprite Sheet' to 'Pixel'."

    [Fact]
    public async Task Step7_CreateRunAnimation_AclsNowHasTwoChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;

        await ctx.AppCommands.AddAnimationChain();
        var idle = ctx.SelectedState.SelectedChain!;
        ctx.AppCommands.RenameChain(idle, "Idle");

        await ctx.AppCommands.AddAnimationChain();
        var run = ctx.SelectedState.SelectedChain!;
        ctx.AppCommands.RenameChain(run, "Run");

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
        var ctx = TestHelpers.SetupFreshAcls();
        var acls = ctx.Acls;
        var chain = TestHelpers.MakeChain(acls, "Run");

        // Doc example: first run frame covers roughly the left half of a 128×128 sprite sheet
        ctx.AppCommands.AddFrameFromPixelBounds(chain, "Running.png", 0, 0, 64, 128, 128, 128);

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
