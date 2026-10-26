using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using Version = SemanticVersioning.Version;

namespace ArmyOfTwo.Server.Updates;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 1_001)]
public sealed class UpdateNotificationService(ISptLogger<UpdateNotificationService> logger) : IOnLoad
{
    private const string ReleasesApiUrl =
        "https://api.github.com/repos/notSENZE/ArmyOfTwo/releases?per_page=20";
    private const string ReleasesUrl =
        "https://github.com/notSENZE/ArmyOfTwo/releases";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var releases = await HttpClient.GetFromJsonAsync<List<GitHubRelease>>(
                ReleasesApiUrl,
                cancellationToken);

            if (!Version.TryParse(ModInfo.Version, out var installedVersion))
            {
                return;
            }

            var latestRelease = releases?
                .Where(release => !release.Draft && !string.IsNullOrWhiteSpace(release.TagName))
                .Select(release => new
                {
                    Release = release,
                    Version = ParseVersion(release.TagName!)
                })
                .Where(candidate => candidate.Version is not null)
                .OrderByDescending(candidate => candidate.Version)
                .FirstOrDefault();

            if (latestRelease?.Version is null || latestRelease.Version <= installedVersion)
            {
                return;
            }

            var downloadUrl = string.IsNullOrWhiteSpace(latestRelease.Release.HtmlUrl)
                ? ReleasesUrl
                : latestRelease.Release.HtmlUrl;

            logger.Warning(
                $"Army of Two: update available: {latestRelease.Version} "
                + $"(installed: {installedVersion}). Download: {downloadUrl}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.Debug("Army of Two: update check timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.Debug($"Army of Two: update check failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            logger.Debug($"Army of Two: GitHub returned an invalid update response: {exception.Message}");
        }
    }

    private static Version? ParseVersion(string tag)
    {
        return Version.TryParse(tag.Trim().TrimStart('v', 'V'), true, out var version)
            ? version
            : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ArmyOfTwo/{ModInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }
    }
}
