using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace MusicPitchSpike;

// Spike for FlatRedBall2 issue #799 — NO FlatRedBall2 involved, pure MonoGame.
//
// Question this answers: does DynamicSoundEffectInstance.Pitch actually change the *effective
// playback rate* on the DesktopGL (OpenAL) backend — i.e. does OpenAL consume queued PCM faster/
// slower when Pitch changes, the way AL_PITCH is documented to work? That's the primitive the
// #799 fix plan depends on (tape-slowdown pitch tied to rate, fed via SubmitBuffer).
//
// Method: generate a continuous synthetic sine tone (no NVorbis/file decode needed — ogg decode
// is already proven elsewhere in MonoGame's own Song loading, so it is not the unknown here).
// Feed fixed-size PCM chunks to a DynamicSoundEffectInstance. Cycle through Pitch = 0, +1, -1
// (XNA semantics: +/-1 = one octave up/down = 2x/0.5x rate). For each phase, count how many
// chunks get consumed in a fixed wall-clock window and compare the resulting rate against the
// expected 2^Pitch multiplier. Prints PASS/FAIL per phase and exits automatically so the result
// can be read from stdout without a human listening.
public class Game1 : Game
{
    private const int SampleRate = 44100;
    private const int ChunkSamples = 4096; // ~92.9 ms of audio per submitted buffer at 44100Hz
    private const double ChunkDurationSeconds = (double)ChunkSamples / SampleRate;
    private const double ToneFrequencyHz = 440.0;
    private const double PhaseSeconds = 3.0;
    private const double WarmupSeconds = 0.5; // discard transient right after a Pitch change

    private static readonly float[] PitchSequence = [0f, 1f, -1f];

    private readonly GraphicsDeviceManager _graphics;
    private readonly Stopwatch _phaseClock = new();
    private DynamicSoundEffectInstance _instance = null!;
    private double _tonePhase;
    private int _pitchIndex;
    private int _chunksThisPhase;
    private bool _warmedUp;
    private bool _finished;
    private bool _anyFailed;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _instance = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Mono);
        _instance.BufferNeeded += OnBufferNeeded;

        // Pre-buffer a few chunks before Play() to avoid an initial underrun.
        for (int i = 0; i < 3; i++)
            _instance.SubmitBuffer(GenerateChunk());

        Console.WriteLine($"[spike] sampleRate={SampleRate} chunkSamples={ChunkSamples} chunkDuration={ChunkDurationSeconds:F4}s");
        Console.WriteLine($"[spike] phase 0: Pitch={PitchSequence[0]}");
        _instance.Pitch = PitchSequence[0];
        _instance.Play();
        _phaseClock.Start();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_finished)
        {
            Exit();
            return;
        }

        double elapsed = _phaseClock.Elapsed.TotalSeconds;

        if (!_warmedUp && elapsed >= WarmupSeconds)
        {
            _warmedUp = true;
            _chunksThisPhase = 0;
            _phaseClock.Restart();
            elapsed = 0;
        }

        if (_warmedUp && elapsed >= PhaseSeconds)
        {
            EvaluatePhase(elapsed);
            _pitchIndex++;

            if (_pitchIndex >= PitchSequence.Length)
            {
                Console.WriteLine(_anyFailed ? "[spike] RESULT: FAIL" : "[spike] RESULT: PASS");
                _finished = true;
            }
            else
            {
                _instance.Pitch = PitchSequence[_pitchIndex];
                Console.WriteLine($"[spike] phase {_pitchIndex}: Pitch={PitchSequence[_pitchIndex]}");
                _warmedUp = false;
                _chunksThisPhase = 0;
                _phaseClock.Restart();
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }

    private void EvaluatePhase(double wallClockSeconds)
    {
        double submittedAudioSeconds = _chunksThisPhase * ChunkDurationSeconds;
        double observedRate = submittedAudioSeconds / wallClockSeconds;
        double expectedRate = Math.Pow(2.0, PitchSequence[_pitchIndex]);
        double relativeError = Math.Abs(observedRate - expectedRate) / expectedRate;
        bool pass = relativeError < 0.15; // 15% tolerance for scheduler/GC jitter
        _anyFailed |= !pass;

        Console.WriteLine(
            $"[spike] phase {_pitchIndex} done: chunks={_chunksThisPhase} wallClock={wallClockSeconds:F3}s " +
            $"observedRate={observedRate:F3}x expectedRate={expectedRate:F3}x " +
            $"{(pass ? "PASS" : "FAIL")}");
    }

    private void OnBufferNeeded(object? sender, EventArgs e)
    {
        _instance.SubmitBuffer(GenerateChunk());
        if (_warmedUp) _chunksThisPhase++;
    }

    private byte[] GenerateChunk()
    {
        var buffer = new byte[ChunkSamples * 2]; // 16-bit mono PCM
        double phaseStep = 2.0 * Math.PI * ToneFrequencyHz / SampleRate;

        for (int i = 0; i < ChunkSamples; i++)
        {
            short sample = (short)(Math.Sin(_tonePhase) * short.MaxValue * 0.25);
            buffer[i * 2] = (byte)(sample & 0xFF);
            buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            _tonePhase += phaseStep;
            if (_tonePhase > 2.0 * Math.PI) _tonePhase -= 2.0 * Math.PI;
        }

        return buffer;
    }
}
