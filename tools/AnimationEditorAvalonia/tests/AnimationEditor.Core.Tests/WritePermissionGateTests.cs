using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class WritePermissionGateTests
{
    [Fact]
    public void AllowsDirectSave_DeniedOrNull_ReturnsFalse()
    {
        Assert.False(WritePermissionGate.AllowsDirectSave("denied"));
        Assert.False(WritePermissionGate.AllowsDirectSave("prompt"));
        Assert.False(WritePermissionGate.AllowsDirectSave(null));
    }

    [Fact]
    public void AllowsDirectSave_Granted_ReturnsTrue()
    {
        var state = "granted";

        Assert.True(WritePermissionGate.AllowsDirectSave(state));
    }

    [Fact]
    public void FormatSaveFailure_IncludesState()
    {
        var state = "denied";

        Assert.Equal("write permission: denied", WritePermissionGate.FormatSaveFailure(state));
    }

    [Fact]
    public void FormatStatusSuffix_IncludesState()
    {
        var state = "prompt";

        Assert.Equal("[write:prompt]", WritePermissionGate.FormatStatusSuffix(state));
    }

    [Fact]
    public void EvaluateSaveFromQueryState_Granted_AllowsSave()
    {
        var (canSave, failure) = WritePermissionGate.EvaluateSaveFromQueryState("granted");

        Assert.True(canSave);
        Assert.Null(failure);
    }

    [Fact]
    public void EvaluateSaveFromQueryState_Denied_BlocksWithDiagnostic()
    {
        var (canSave, failure) = WritePermissionGate.EvaluateSaveFromQueryState("denied");

        Assert.False(canSave);
        Assert.Equal("write permission: denied", failure);
    }
}
