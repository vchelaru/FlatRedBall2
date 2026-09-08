using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Writes the empty animation chain list that "New Animation File" creates (issue #1018). Stream-
/// based so desktop and the browser share it -- one writes a <c>FileStream</c>, the other an
/// <see cref="IEditorFile"/> stream.
/// </summary>
public static class NewAnimationFileWriter
{
    /// <summary>
    /// Writes an empty chain list to <paramref name="stream"/>, XML or JSON per
    /// <paramref name="fileName"/>'s extension. The stream is left open for the caller to dispose.
    /// </summary>
    public static void WriteEmpty(Stream stream, string fileName)
    {
        // Pixel, not the AnimationChainListSave default of UV: opening a UV file runs the
        // convert-to-pixel prompt, which is nonsense for a file the editor just created empty.
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };

        if (string.Equals(new FilePath(fileName).Extension, NewAnimationFileNaming.JsonExtension,
                StringComparison.OrdinalIgnoreCase))
            save.SaveJson(stream);
        else
            save.Save(stream);
    }
}
