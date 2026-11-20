using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Outcome of <see cref="EditSession.OpenAsync"/>: the session, or null with the reason in <see cref="Result"/>.</summary>
public sealed record EditSessionOpen(EditSession? Session, EditResult Result);

/// <summary>Stub, replaced by the implementation commit.</summary>
public sealed class EditSession
{
    public IProjectManager ProjectManager => throw new NotImplementedException();
    public IAppCommands Commands => throw new NotImplementedException();
    public AnimationChainListSave Document => throw new NotImplementedException();
    public static Task<EditSessionOpen> OpenAsync(string path) => throw new NotImplementedException();
    public EditResult Save() => throw new NotImplementedException();
}

/// <summary>Stub.</summary>
public sealed record AddressResult<T>(T? Value, string? Error) where T : class;

/// <summary>Stub.</summary>
public static class EditAddress
{
    public static AddressResult<AnimationChainSave> ResolveChain(AnimationChainListSave doc, string address) => throw new NotImplementedException();
    public static AddressResult<AnimationFrameSave> ResolveFrame(AnimationChainListSave doc, string address) => throw new NotImplementedException();
    public static AddressResult<ShapeSave> ResolveShape(AnimationChainListSave doc, string address) => throw new NotImplementedException();
}

/// <summary>Stub.</summary>
public enum CheckSeverity { Error, Warning }

/// <summary>Stub.</summary>
public sealed record CheckIssue(CheckSeverity Severity, string Code, string Message);

/// <summary>Stub.</summary>
public sealed record CheckReport(IReadOnlyList<CheckIssue> Issues)
{
    public static IReadOnlyList<CheckIssue> NewErrors(CheckReport before, CheckReport after) => throw new NotImplementedException();
}
