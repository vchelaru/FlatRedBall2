using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.Utilities;

/// <summary>Builds the text a user pastes to reference an animation from outside the editor.</summary>
public static class QualifiedName
{
    /// <summary>"<c>Walk in C:/game/Player.achj</c>": the animation name, then its file with forward slashes.</summary>
    public static string Format(string filePath, string animationName) =>
        $"{animationName} in {new FilePath(filePath).FullPath}";
}
