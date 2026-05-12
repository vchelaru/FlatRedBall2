using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AppCommandsSetFrameTextureTests
{
    // ── SetFrameTextureName — basic assignment ────────────────────────────────

    [Fact]
    public void SetFrameTextureName_SetsTextureNameOnFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave();

        ctx.AppCommands.SetFrameTextureName(frame, "hero.png");

        Assert.Equal("hero.png", frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_CanOverwriteExistingName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { TextureName = "old.png" };

        ctx.AppCommands.SetFrameTextureName(frame, "new.png");

        Assert.Equal("new.png", frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_CanClearToNull()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { TextureName = "hero.png" };

        ctx.AppCommands.SetFrameTextureName(frame, null);

        Assert.Null(frame.TextureName);
    }

    [Fact]
    public void SetFrameTextureName_CanClearToEmptyString()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame = new AnimationFrameSave { TextureName = "hero.png" };

        ctx.AppCommands.SetFrameTextureName(frame, "");

        Assert.Equal("", frame.TextureName);
    }

    // ── Guard: null frame ─────────────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureName_NullFrame_DoesNotThrow()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        // Should be a silent no-op
        ctx.AppCommands.SetFrameTextureName(null!, "hero.png");
    }

    // ── Event firing ──────────────────────────────────────────────────────────

    [Fact]
    public void SetFrameTextureName_FiresAnimationChainsChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame   = new AnimationFrameSave();
        bool fired  = false;
        ctx.ApplicationEvents.AnimationChainsChanged += () => fired = true;

        ctx.AppCommands.SetFrameTextureName(frame, "run.png");

        Assert.True(fired);
    }

    [Fact]
    public void SetFrameTextureName_FiresRefreshFrameNodeRequested_WithCorrectFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var frame             = new AnimationFrameSave();
        AnimationFrameSave? received = null;
        ctx.AppCommands.RefreshFrameNodeRequested += f => received = f;

        ctx.AppCommands.SetFrameTextureName(frame, "idle.png");

        Assert.Same(frame, received);
    }

    [Fact]
    public void SetFrameTextureName_NullFrame_DoesNotFireEvents()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        bool changed = false;
        ctx.ApplicationEvents.AnimationChainsChanged += () => changed = true;

        ctx.AppCommands.SetFrameTextureName(null!, "hero.png");

        Assert.False(changed);
    }
}
