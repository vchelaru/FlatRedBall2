namespace AnimationEditor.Core.IO;

/// <summary>
/// Registers the editor as the operating-system default handler for <c>.achx</c> files
/// and reports whether it currently is. Implementations are platform-specific (Windows
/// registry, macOS Launch Services, Linux XDG); platforms without an implementation use
/// <see cref="NullFileAssociationService"/> and report <see cref="IsSupported"/> as false.
/// </summary>
public interface IFileAssociationService
{
    /// <summary>
    /// Whether file association is implemented on the current platform. When false, callers
    /// should neither call <see cref="IsDefault"/>/<see cref="RegisterAsDefault"/> nor prompt
    /// the user.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Whether this editor is the current default handler for <c>.achx</c> files — i.e. our
    /// ProgId is registered and its open command targets this build's executable. Only
    /// meaningful when <see cref="IsSupported"/> is true.
    /// </summary>
    bool IsDefault();

    /// <summary>
    /// Detailed association state for settings UI. Returns
    /// <see cref="AchxFileAssociationStatus.NotSupported"/> when <see cref="IsSupported"/> is false.
    /// </summary>
    AchxFileAssociationStatus GetStatus();

    /// <summary>
    /// Registers the editor's file-type association and surfaces the OS confirmation UI.
    /// Modern Windows blocks an app from silently forcing itself as the default, so this
    /// registers the association and then opens the system default-apps settings for the
    /// user to confirm. No-op when <see cref="IsSupported"/> is false.
    /// </summary>
    void RegisterAsDefault();
}
