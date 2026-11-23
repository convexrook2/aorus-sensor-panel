using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace SensorPanel;

internal sealed record PanelRelease(Version Version, Uri Page);

internal static class PanelUpdateService
{
    private const string Repository = "convexrook2/aorus-sensor-panel";
    private const string Installer = "AORUS-Sensor-Panel-FAN-MADE-Setup.exe";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = 1024 * 1024
    };

    internal static Version CurrentVersion { get; } = typeof(PanelUpdateService).Assembly.GetName().Version!;

    internal static async Task<PanelRelease?> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"AorusSensorPanel/{CurrentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ReadRelease(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            return null;
        }
    }

    internal static PanelRelease? ReadRelease(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        string tag = tagElement.GetString()!;
        var number = tag.AsSpan();
        if (number.StartsWith("v", StringComparison.OrdinalIgnoreCase)) number = number[1..];
        if (number.IsEmpty || number.ContainsAnyExcept("0123456789.") ||
            !Version.TryParse(number, out var version) || version.Build < 0 ||
            !assets.EnumerateArray().Any(IsInstaller))
            return null;

        // Assembly versions have four parts. A three-part tag denotes revision zero.
        var comparable = new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
        return new(comparable, new Uri($"https://github.com/{Repository}/releases/tag/{tag}"));
    }

    private static bool IsInstaller(JsonElement asset) => asset.ValueKind == JsonValueKind.Object &&
        asset.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.ValueEquals(Installer) &&
        asset.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String && state.ValueEquals("uploaded");

    internal static string Label(Version version) => version.ToString(version.Revision == 0 ? 3 : 4);
}
