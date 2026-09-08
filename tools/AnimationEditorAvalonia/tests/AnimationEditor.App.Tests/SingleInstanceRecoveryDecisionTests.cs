using AnimationEditor.App.Services;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Covers the pure decision table a second launch uses once it knows whether it reached the
/// primary instance's IPC pipe (#1049). The OS-specific classification that produces
/// <see cref="PrimaryInstanceHangResult"/> (P/Invoke <c>IsHungAppWindow</c>, process lookup) is
/// thin wiring tested separately/manually — see <c>PrimaryInstanceHangChecker</c>'s doc comment.
/// </summary>
public class SingleInstanceRecoveryDecisionTests
{
    [Fact]
    public void Decide_PipeReached_ReturnsSilentExit()
    {
        var action = SingleInstanceRecoveryDecision.Decide(
            reachedPrimary: true, hangResult: PrimaryInstanceHangResult.Hung);

        Assert.Equal(SingleInstanceRecoveryAction.SilentExit, action);
    }

    [Fact]
    public void Decide_PipeUnreachableAndBusyNotHung_ReturnsShowBusyMessage()
    {
        var action = SingleInstanceRecoveryDecision.Decide(
            reachedPrimary: false, hangResult: PrimaryInstanceHangResult.BusyNotHung);

        Assert.Equal(SingleInstanceRecoveryAction.ShowBusyMessage, action);
    }

    [Fact]
    public void Decide_PipeUnreachableAndHung_ReturnsOfferRestart()
    {
        var action = SingleInstanceRecoveryDecision.Decide(
            reachedPrimary: false, hangResult: PrimaryInstanceHangResult.Hung);

        Assert.Equal(SingleInstanceRecoveryAction.OfferRestart, action);
    }
}
