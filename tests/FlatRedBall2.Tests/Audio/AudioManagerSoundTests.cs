using System;
using System.Threading;
using System.Threading.Tasks;
using FlatRedBall2.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Audio;

public class AudioManagerSoundTests
{
    private class FakeSoundInstance : ISoundInstance
    {
        public SoundState State { get; set; } = SoundState.Stopped;
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float Pan { get; set; }
        public bool IsLooped { get; set; }
        public bool Disposed { get; private set; }
        public int StopCallCount { get; private set; }

        public void Play() => State = SoundState.Playing;
        public void Stop() { StopCallCount++; State = SoundState.Stopped; }
        public void Dispose() => Disposed = true;
    }

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private static (SoundHandle Handle, FakeSoundInstance Instance) Play(
        AudioManager audio, object key, float volume = 1f, float pitch = 0f, float pan = 0f,
        bool loop = false, bool playAlways = false)
    {
        var instance = new FakeSoundInstance();
        var handle = audio.PlayInstance(key, () => instance, volume, pitch, pan, loop, playAlways);
        return (handle, instance);
    }

    [Fact]
    public void Play_ReturnsHandleThatReportsPlaying()
    {
        var audio = new AudioManager();

        var (handle, _) = Play(audio, new object());

        handle.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void Play_AppliesMasterVolumePitchPanAndLoop()
    {
        var audio = new AudioManager { SoundVolume = 0.5f };

        var (_, instance) = Play(audio, new object(), volume: 0.8f, pitch: 2f, pan: -0.5f, loop: true);

        instance.Volume.ShouldBe(0.4f, 0.0001f);
        instance.Pitch.ShouldBe(1f);
        instance.Pan.ShouldBe(-0.5f);
        instance.IsLooped.ShouldBeTrue();
    }

    [Fact]
    public void Stop_StopsInstanceAndHandleReportsNotPlaying()
    {
        var audio = new AudioManager();
        var (handle, instance) = Play(audio, new object(), loop: true);

        handle.Stop();

        instance.StopCallCount.ShouldBe(1);
        handle.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Handle_AfterUpdateDisposesStoppedInstance_IsSafeToQueryAndStop()
    {
        var audio = new AudioManager();
        var (handle, instance) = Play(audio, new object());
        instance.State = SoundState.Stopped; // finished naturally

        audio.Update();

        instance.Disposed.ShouldBeTrue();
        handle.IsPlaying.ShouldBeFalse();
        handle.Stop(); // must not touch the disposed instance
        instance.StopCallCount.ShouldBe(0);
    }

    [Fact]
    public void Play_SoundDisabled_ReturnsInertHandle()
    {
        var audio = new AudioManager { SoundEnabled = false };

        var (handle, instance) = Play(audio, new object());

        handle.IsPlaying.ShouldBeFalse();
        instance.State.ShouldBe(SoundState.Stopped);
        audio.ConcurrentSounds.ShouldBe(0);
        handle.Stop();
    }

    [Fact]
    public void Play_SameKeySameFrame_SecondIsDeduped()
    {
        var audio = new AudioManager();
        var key = new object();

        Play(audio, key);
        var (second, _) = Play(audio, key);

        second.IsPlaying.ShouldBeFalse();
        audio.ConcurrentSounds.ShouldBe(1);
    }

    [Fact]
    public void Play_PlayAlways_SameKeySameFrame_StacksInstances()
    {
        var audio = new AudioManager();
        var key = new object();

        Play(audio, key, pitch: 0f, playAlways: true);
        var (second, _) = Play(audio, key, pitch: 0.5f, playAlways: true);

        second.IsPlaying.ShouldBeTrue();
        audio.ConcurrentSounds.ShouldBe(2);
    }

    [Fact]
    public void Play_PlayAlways_StillHonorsMaxConcurrentSounds()
    {
        var audio = new AudioManager { MaxConcurrentSounds = 1 };
        var key = new object();

        Play(audio, key, playAlways: true);
        var (second, _) = Play(audio, key, playAlways: true);

        second.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Play_PlayAlways_StillHonorsSoundEnabled()
    {
        var audio = new AudioManager { SoundEnabled = false };

        var (handle, _) = Play(audio, new object(), playAlways: true);

        handle.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void Play_SameKeyNextFrame_NotDeduped()
    {
        var audio = new AudioManager();
        var key = new object();
        Play(audio, key);

        audio.Update();
        var (second, _) = Play(audio, key);

        second.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void IsPlayingByKey_SeesInstanceUntilStopped()
    {
        var audio = new AudioManager();
        var key = new object();
        var (handle, _) = Play(audio, key);

        audio.IsPlayingKey(key).ShouldBeTrue();

        handle.Stop();

        audio.IsPlayingKey(key).ShouldBeFalse();
    }

    // ── PlayAsync ──────────────────────────────────────────────────────────────

    private static void Tick(TimeManager time, double seconds)
    {
        time.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)), false);
        time.DoTaskLogic();
    }

    private static (AudioManager Audio, TimeManager Time) MakeWired()
    {
        var time = new TimeManager();
        return (new AudioManager { Time = time }, time);
    }

    [Fact]
    public void PlayAsync_CompletesWhenDurationElapses()
    {
        var (audio, time) = MakeWired();

        var task = audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
            1f, 0f, 0f, false, CancellationToken.None);

        task.IsCompleted.ShouldBeFalse();
        Tick(time, 0.5);
        task.IsCompleted.ShouldBeFalse();
        Tick(time, 0.5);
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void PlayAsync_StartsTheSound()
    {
        var (audio, _) = MakeWired();
        var instance = new FakeSoundInstance();

        audio.PlayAsyncInstance(new object(), OneSecond, () => instance, 1f, 0f, 0f, false, CancellationToken.None);

        instance.State.ShouldBe(SoundState.Playing);
    }

    [Fact]
    public void PlayAsync_PositivePitch_ShortensWait()
    {
        var (audio, time) = MakeWired();

        // One octave up plays at 2x rate, so a 1s clip lasts 0.5s.
        var task = audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
            1f, 1f, 0f, false, CancellationToken.None);

        Tick(time, 0.5);
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void PlayAsync_NegativePitch_LengthensWait()
    {
        var (audio, time) = MakeWired();

        var task = audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
            1f, -1f, 0f, false, CancellationToken.None);

        Tick(time, 1.0);
        task.IsCompleted.ShouldBeFalse();
        Tick(time, 1.0);
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void PlayAsync_SoundDisabled_StillWaitsFullDuration()
    {
        var (audio, time) = MakeWired();
        audio.SoundEnabled = false;

        var task = audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
            1f, 0f, 0f, false, CancellationToken.None);

        task.IsCompleted.ShouldBeFalse();
        Tick(time, 1.0);
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void PlayAsync_TokenCancelled_CancelsTask()
    {
        var (audio, time) = MakeWired();
        using var cts = new CancellationTokenSource();

        var task = audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
            1f, 0f, 0f, false, cts.Token);
        cts.Cancel();
        Tick(time, 0.1);

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void PlayAsync_TimeNotWired_Throws()
    {
        var audio = new AudioManager();

        Should.Throw<InvalidOperationException>(() =>
            audio.PlayAsyncInstance(new object(), OneSecond, () => new FakeSoundInstance(),
                1f, 0f, 0f, false, CancellationToken.None));
    }

    [Fact]
    public void Engine_WiresAudioToItsOwnTimeManager()
    {
        var engine = new FlatRedBallService();

        engine.Audio.Time.ShouldBeSameAs(engine.Time);
    }
}
