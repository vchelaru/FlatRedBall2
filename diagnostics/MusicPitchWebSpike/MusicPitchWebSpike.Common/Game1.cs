using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace MusicPitchWebSpike;

// Interactive counterpart to diagnostics/MusicPitchSpike, for issue #799 — confirms
// DynamicSoundEffectInstance.Pitch is audible (not just measurable) on KNI's BlazorGL
// backend (Web Audio playbackRate), which the desktop spike could not exercise.
//
// No FlatRedBall2 engine involved — same isolation goal as the desktop spike, just on KNI.
// No content/pipeline needed either: every tone is generated synthetically at runtime.
//
// Press 1 / 2 / 3 to play a short tone at a different Pitch. All three should be clearly
// audible at different pitches if DynamicSoundEffectInstance.Pitch works on this backend.
public class Game1 : Game
{
    // KNI's BlazorGL backend throws if this doesn't match the browser's AudioContext sample
    // rate (48000 on this machine/Chrome) — unlike desktop OpenAL, which resamples freely from
    // any source rate. The real #799 feature will need to read the actual AudioContext rate at
    // runtime rather than hardcode one value; there's no single rate guaranteed across browsers.
    private const int SampleRate = 48000;
    private const int ChunkSamples = 4096;
    private const double ToneFrequencyHz = 440.0;
    private const double NoteSeconds = 0.6;

    private static readonly (Keys Key, float Pitch, string Label)[] Notes =
    [
        (Keys.D1, -1f, "Pitch -1 (down an octave)"),
        (Keys.D2, 0f, "Pitch 0 (unchanged)"),
        (Keys.D3, 1f, "Pitch +1 (up an octave)"),
    ];

    private readonly GraphicsDeviceManager _graphics;
    private readonly List<DynamicSoundEffectInstance> _active = new();
    private KeyboardState _previousKeyboard;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // KNI BlazorGL needs this so the back buffer tracks the canvas DOM size.
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        foreach (var (key, pitch, label) in Notes)
        {
            if (keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key))
            {
                PlayNote(pitch);
                Console.WriteLine($"[web-spike] key {key} -> {label}");
            }
        }

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].State == SoundState.Stopped)
            {
                _active[i].Dispose();
                _active.RemoveAt(i);
            }
        }

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.DarkSlateBlue);
        base.Draw(gameTime);
    }

    // Submits the whole note up front (a few hundred ms fits easily in one go) — no
    // BufferNeeded feed loop needed for a short one-shot note, unlike the desktop spike's
    // continuous stream.
    private void PlayNote(float pitch)
    {
        var instance = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Mono)
        {
            Pitch = pitch,
        };

        double phase = 0;
        double phaseStep = 2.0 * Math.PI * ToneFrequencyHz / SampleRate;
        int remaining = (int)(NoteSeconds * SampleRate);

        while (remaining > 0)
        {
            int count = Math.Min(ChunkSamples, remaining);
            var buffer = new byte[count * 2]; // 16-bit mono PCM

            for (int i = 0; i < count; i++)
            {
                short sample = (short)(Math.Sin(phase) * short.MaxValue * 0.25);
                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                phase += phaseStep;
                if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;
            }

            instance.SubmitBuffer(buffer);
            remaining -= count;
        }

        instance.Play();
        _active.Add(instance);
    }
}
