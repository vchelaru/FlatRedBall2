namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Arms suppression of the next Alt KeyUp after an Alt+Arrow tree reorder so Avalonia's
/// menu does not steal focus on Alt release (issue #488).
/// </summary>
public sealed class AltMenuActivationSuppressor
{
    private bool _suppressNextAltKeyUp;

    public void ArmFromAltArrowReorder() => _suppressNextAltKeyUp = true;

    /// <summary>
    /// Returns true when the caller should mark the Alt KeyUp handled to block menu activation.
    /// Clears the arm on consumption.
    /// </summary>
    public bool TryConsumeIfArmed()
    {
        if (!_suppressNextAltKeyUp) return false;
        _suppressNextAltKeyUp = false;
        return true;
    }
}
