using System.IO;
using AsepriteDotNet.Aseprite;

namespace FlatRedBall2.Content.Aseprite;

/// <summary>
/// Loads Aseprite (.ase / .aseprite) files from disk using the AsepriteDotNet library.
/// </summary>
public static class AsepriteFileLoader
{
    /// <param name="absoluteFileName">Full path to the .ase or .aseprite file.</param>
    public static AsepriteFile Load(string absoluteFileName)
    {
        string name = Path.GetFileNameWithoutExtension(absoluteFileName);
        return AsepriteDotNet.IO.AsepriteFileLoader.FromFile(absoluteFileName, preMultiplyAlpha: true);
    }
}
