using System.Collections.Generic;

namespace AnimationEditor.Core.Models
{
    /// <summary>A wireframe camera: the screen position of texture pixel (0,0) and the zoom factor.</summary>
    public readonly record struct TextureCamera(float PanX, float PanY, float Zoom);

    /// <summary>
    /// The one place a tab's wireframe camera lives. Each <see cref="TabEntry"/> owns one, and the wireframe reads
    /// and writes the memory of whichever tab is active, so a camera is only ever chosen from one source: the
    /// per-texture camera the tab last left on that texture, else the camera seeded from its companion file the
    /// first time the tab was shown, else the control centres the texture.
    /// </summary>
    public sealed class TextureCameraMemory
    {
        private readonly Dictionary<string, TextureCamera> _byTexture = new();
        private TextureCamera? _initial;

        /// <summary>True once this tab has been seeded, so a later companion-file load (a reload from disk) never moves the camera.</summary>
        public bool Seeded { get; private set; }

        /// <summary>Records the camera the tab had on <paramref name="texture"/> (a normalized path) when it left it.</summary>
        public void Remember(string texture, TextureCamera camera)
        {
            _byTexture[texture] = camera;
            Seeded = true;
        }

        /// <summary>Whether <paramref name="camera"/> is the one recorded for <paramref name="texture"/>.</summary>
        public bool Remembers(string texture, TextureCamera camera) =>
            _byTexture.TryGetValue(texture, out var remembered) && remembered == camera;

        /// <summary>
        /// Gives a tab that has never been shown the camera its companion file saved. Applied to the first texture
        /// the tab shows. Ignored once the tab has been seeded or has remembered any camera.
        /// </summary>
        public void Seed(TextureCamera camera)
        {
            if (Seeded) return;
            _initial = camera;
            Seeded = true;
        }

        /// <summary>
        /// The camera to show <paramref name="texture"/> with, or null when the texture should be centred.
        /// <c>fromSeed</c> is true for the one-time seeded camera, which was never clamped to a viewport.
        /// </summary>
        public TextureCamera? Take(string texture, out bool fromSeed)
        {
            fromSeed = false;
            if (_byTexture.TryGetValue(texture, out var remembered))
                return remembered;
            if (_initial is { } seed)
            {
                _initial = null;
                fromSeed = true;
                return seed;
            }
            return null;
        }
    }
}
