using System;

namespace AnimationEditor.Core.Hotkeys;

/// <summary>
/// UI-framework-independent modifier flags for hotkey matching. <see cref="Command"/> unifies
/// the platform command modifier — Control on Windows/Linux, Meta (⌘) on macOS — into one flag,
/// so a gesture that requires <see cref="Command"/> matches either without caring which OS it's
/// running on. Callers translate the host UI framework's native modifier enum into this one.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None    = 0,
    Shift   = 1 << 0,
    Alt     = 1 << 1,
    Command = 1 << 2,
}
