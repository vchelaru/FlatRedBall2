using System.Net.Http;
using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Update;

/// <summary>
/// Hits the GitHub Releases API for this repo. Anonymous requests get a 60/hr-per-IP
/// rate limit — <see cref="UpdateCheckPolicy"/> keeps callers well under that by caching
/// the result for 24h — and GitHub rejects requests with no User-Agent header, so one is
/// always set.
/// </summary>
public sealed class HttpGitHubReleaseClient : IGitHubReleaseClient
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/vchelaru/FlatRedBall2/releases/latest";

    private readonly HttpClient _httpClient;

    public HttpGitHubReleaseClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FlatRedBall2-AnimationEditor");
        }
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var json = await _httpClient.GetStringAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
        var response = System.Text.Json.JsonSerializer.Deserialize<ReleaseResponse>(json);

        if (response?.HtmlUrl is null)
            return null;

        return new GitHubReleaseInfo { PublishedAt = response.PublishedAt, HtmlUrl = response.HtmlUrl };
    }

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
