namespace AnimationEditor.Core.Update;

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
}
