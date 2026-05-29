using FlatRedBall.AnimationChain;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

public class AnimationPlayerTests
{
    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static AnimationChainList MakeList(params (string name, double[] frameLengths)[] chains)
    {
        var list = new AnimationChainList();
        foreach (var (name, frameLengths) in chains)
        {
            var chain = new FlatRedBall.AnimationChain.AnimationChain { Name = name };
            foreach (var len in frameLengths)
                chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(len) });
            list.Add(chain);
        }
        return list;
    }

    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    // ─── Play ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Play_ByName_SetsFirstFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer(list);
        player.Play("Run");
        Assert.Equal(list["Run"]![0], player.CurrentFrame);
    }

    [Fact]
    public void Play_UnknownName_IsNoOp()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer(list);
        player.Play("Run");
        player.Play("DoesNotExist"); // must not throw
        Assert.Equal("Run", player.CurrentChain!.Name);
    }

    [Fact]
    public void Play_SameChainTwice_DoesNotRestart()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer(list);
        player.Play("Run");
        player.Update(Sec(0.15)); // advances past first frame
        var frameAfterAdvance = player.CurrentFrame;

        player.Play("Run"); // same chain — should not restart
        Assert.Same(frameAfterAdvance, player.CurrentFrame);
    }

    [Fact]
    public void Play_DifferentChain_Restarts()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }), ("Idle", new[] { 0.2 }));
        var player = new AnimationPlayer(list);
        player.Play("Run");
        player.Update(Sec(0.15));
        player.Play("Idle");
        Assert.Equal("Idle", player.CurrentChain!.Name);
        Assert.Equal(list["Idle"]![0], player.CurrentFrame);
    }

    [Fact]
    public void Play_ByChainReference_SameInstance_DoesNotRestart()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer(list);
        var chain = list["Run"]!;
        player.Play(chain);
        player.Update(Sec(0.15));
        var frameBefore = player.CurrentFrame;
        player.Play(chain);
        Assert.Same(frameBefore, player.CurrentFrame);
    }

    // ─── Update / frame advancement ─────────────────────────────────────────────

    [Fact]
    public void Update_AdvancesToNextFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer(list);
        player.Play("Run");
        player.Update(Sec(0.15));
        Assert.Equal(list["Run"]![1], player.CurrentFrame);
    }

    [Fact]
    public void Update_Looping_WrapsToFirstFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 })); // total = 0.2s
        var player = new AnimationPlayer(list) { IsLooping = true };
        player.Play("Run");
        player.Update(Sec(0.25)); // past end of loop
        // 0.25 mod 0.2 = 0.05 → still in first frame (0–0.1)
        Assert.Equal(list["Run"]![0], player.CurrentFrame);
    }

    [Fact]
    public void Update_NonLooping_StopsAtLastFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer(list) { IsLooping = false };
        player.Play("Run");
        player.Update(Sec(0.5)); // well past end
        Assert.Equal(list["Run"]![1], player.CurrentFrame);
        Assert.False(player.Animate);
    }

    [Fact]
    public void Update_NonLooping_RaisesAnimationFinished()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer(list) { IsLooping = false };
        player.Play("Run");

        bool fired = false;
        player.AnimationFinished += () => fired = true;
        player.Update(Sec(0.2));

        Assert.True(fired);
    }

    [Fact]
    public void Update_AnimationFinished_RaisedOnce()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer(list) { IsLooping = false };
        player.Play("Run");

        int count = 0;
        player.AnimationFinished += () => count++;
        player.Update(Sec(0.2));
        player.Update(Sec(0.2)); // second update — Animate is false, should not re-fire
        Assert.Equal(1, count);
    }

    [Fact]
    public void Update_AnimateIsFalse_DoesNotAdvance()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer(list) { Animate = false };
        player.Play("Run");
        player.Animate = false;
        player.Update(Sec(0.2));
        Assert.Equal(list["Run"]![0], player.CurrentFrame);
    }

    [Fact]
    public void AnimationSpeed_HalfSpeed_TakesDoubleTime()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer(list) { AnimationSpeed = 0.5f };
        player.Play("Run");
        player.Update(Sec(0.15)); // at half speed this is only 0.075s of animation time
        Assert.Equal(list["Run"]![0], player.CurrentFrame); // still on first frame
    }

    // ─── Edge cases ──────────────────────────────────────────────────────────────

    [Fact]
    public void CurrentFrame_BeforePlay_IsNull()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer(list);
        Assert.Null(player.CurrentFrame);
    }

    [Fact]
    public void Update_NoAnimation_DoesNotThrow()
    {
        var list = new AnimationChainList();
        var player = new AnimationPlayer(list);
        player.Update(Sec(0.1)); // nothing playing — must not throw
    }

    [Fact]
    public void Update_EmptyChain_DoesNotThrow()
    {
        var list = MakeList(("Empty", Array.Empty<double>()));
        var player = new AnimationPlayer(list);
        player.Play("Empty");
        player.Update(Sec(0.1));
    }
}
