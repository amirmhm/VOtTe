using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using VoxPilot.Models;

namespace VoxPilot.Services;

public sealed class OpenRouterClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://openrouter.ai/api/v1/"),
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<IReadOnlyList<ModelOption>> GetAudioModelsAsync(string? apiKey, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "models?output_modalities=transcription&sort=latency-low-to-high");
        AddHeaders(request, apiKey);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var result = new List<ModelOption>();
        if (!document.RootElement.TryGetProperty("data", out var data)) return result;

        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idElement)) continue;
            var id = idElement.GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            result.Add(new ModelOption(name ?? id, id));
        }
        return result;
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string model, string? language,
        string apiKey, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input_audio"] = new { data = Convert.ToBase64String(wavAudio), format = "wav" },
            ["temperature"] = 0
        };
        if (!string.IsNullOrWhiteSpace(language) && language != "auto") payload["language"] = language;

        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions")
        {
            Content = JsonContent.Create(payload)
        };
        AddHeaders(request, apiKey);
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (document.RootElement.TryGetProperty("text", out var textElement))
            return textElement.GetString()?.Trim() ?? string.Empty;

        throw new InvalidOperationException("OpenRouter returned no transcript.");
    }

    private static void AddHeaders(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/voxpilot-app");
        request.Headers.TryAddWithoutValidation("X-Title", "VoxPilot");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    throw new OpenRouterException(
                        $"OpenRouter error {(int)response.StatusCode}: {message.GetString() ?? response.ReasonPhrase}");
                if (error.ValueKind == JsonValueKind.String)
                    throw new OpenRouterException(
                        $"OpenRouter error {(int)response.StatusCode}: {error.GetString() ?? response.ReasonPhrase}");
            }
        }
        catch (JsonException) { }
        throw new OpenRouterException($"OpenRouter error {(int)response.StatusCode}: {response.ReasonPhrase}.");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class OpenRouterException(string message) : Exception(message);
