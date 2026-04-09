using System.Net.Http;
using System.Text.Json;

namespace CatchCatch.Services;

public class UpdateChecker
{
    private const string ApiUrl = "https://api.github.com/repos/HongChaeMin/catch-catch/releases/latest";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public string? LatestVersion { get; private set; }
    public string? DownloadUrl { get; private set; }
    public bool HasUpdate { get; private set; }

    public async Task CheckAsync()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("CatchCatch/1.0");
            var json = await Http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
            if (string.IsNullOrEmpty(tag)) return;

            LatestVersion = tag;

            var currentVersion = typeof(UpdateChecker).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            HasUpdate = string.Compare(tag, currentVersion, StringComparison.Ordinal) > 0;

            // Find .exe or .zip asset for Windows
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        DownloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            // Fallback to release page
            DownloadUrl ??= root.GetProperty("html_url").GetString();
        }
        catch
        {
            // silently fail
        }
    }
}
