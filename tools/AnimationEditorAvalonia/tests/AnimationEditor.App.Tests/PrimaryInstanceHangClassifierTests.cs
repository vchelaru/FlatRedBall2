using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// What a second launch concludes about the running AnimationEditor from the OS's view of its window. A primary
/// that is alive but has no window used to read as "busy" forever, so the user got a dialog that never rescued them.
/// </summary>
public class PrimaryInstanceHangClassifierTests
{
    private static readonly TimeSpan Old = PrimaryInstanceHangClassifier.StartupGrace + TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Young = TimeSpan.FromSeconds(3);

    [Fact]
    public void WindowFoundAndHung_IsHung() =>
        Assert.Equal(PrimaryInstanceHangResult.Hung, PrimaryInstanceHangClassifier.Classify(true, true, Old));

    [Fact]
    public void WindowFoundAndResponding_IsBusyNotHung() =>
        Assert.Equal(PrimaryInstanceHangResult.BusyNotHung, PrimaryInstanceHangClassifier.Classify(true, false, Old));

    [Fact]
    public void NoWindowAfterTheGracePeriod_IsNoWindow() =>
        Assert.Equal(PrimaryInstanceHangResult.NoWindow, PrimaryInstanceHangClassifier.Classify(false, false, Old));

    [Fact]
    public void NoWindowYetButStillStarting_IsBusyNotHung() =>
        Assert.Equal(PrimaryInstanceHangResult.BusyNotHung, PrimaryInstanceHangClassifier.Classify(false, false, Young));

    [Fact]
    public void NoWindowAndUnknownAge_IsBusyNotHung() =>
        Assert.Equal(PrimaryInstanceHangResult.BusyNotHung, PrimaryInstanceHangClassifier.Classify(false, false, null));
}
