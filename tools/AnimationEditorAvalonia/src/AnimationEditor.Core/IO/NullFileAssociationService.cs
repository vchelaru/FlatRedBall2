namespace AnimationEditor.Core.IO;

/// <summary>
/// No-op <see cref="IFileAssociationService"/> for platforms without a file-association
/// implementation (currently macOS and Linux). Reports <see cref="IsSupported"/> as false
/// so <see cref="DefaultHandlerPromptDecider.ShouldPrompt"/> never offers the prompt.
/// </summary>
public sealed class NullFileAssociationService : IFileAssociationService
{
    public bool IsSupported => false;

    public bool IsDefault() => false;

    public AchxFileAssociationStatus GetStatus() => AchxFileAssociationStatus.NotSupported;

    public void RegisterAsDefault() { }
}
