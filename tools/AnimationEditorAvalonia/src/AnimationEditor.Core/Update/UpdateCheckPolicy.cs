namespace AnimationEditor.Core.Update;

/// <summary>
/// Rate-limits how often the startup check hits the GitHub API — an unauthenticated caller
/// gets 60 requests/hour per IP, and re-checking on every launch could burn through that
/// across many users sharing an egress IP (office/CI network). A forced/manual recheck
/// (e.g. opening the About dialog) bypasses this by simply not consulting it.
/// </summary>
public static class UpdateCheckPolicy
{
    public static readonly TimeSpan CacheWindow = TimeSpan.FromHours(24);

    public static bool ShouldCheck(DateTime? lastCheckUtc, DateTime nowUtc) =>
        lastCheckUtc is null || nowUtc - lastCheckUtc.Value >= CacheWindow;
}
