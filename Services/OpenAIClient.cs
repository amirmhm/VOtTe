using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VoxPilot.Models;

namespace VoxPilot.Services;

public sealed class OpenAIClient : IDisposable
{
    private static readonly IReadOnlyList<ModelOption> TranscriptionModels =
    [
        new("GPT-4o mini Transcribe", "gpt-4o-mini-transcribe"),
        new("GPT-4o Transcribe", "gpt-4o-transcribe"),
        new("Whisper", "whisper-1")
    ];

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://api.openai.com/v1/"),
        Timeout = TimeSpan.FromSeconds(90)
    };

    public Task<IReadOnlyList<ModelOption>> GetAudioModelsAsync() =>
        Task.FromResult(TranscriptionModels);

    public async Task<string> TranscribeAsync(byte[] wavAudio, string model, string? language,
        string apiKey, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var audio = new ByteArrayContent(wavAudio);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "recording.wav");
        form.Add(new StringContent(model), "model");
        if (!string.IsNullOrWhiteSpace(language) && language != "auto")
            form.Add(new StringContent(language), "language");

        using var request = new HttpRequestMessage(HttpMethod.Post, "audio/transcriptions")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (document.RootElement.TryGetProperty("text", out var textElement))
            return textElement.GetString()?.Trim() ?? string.Empty;

        throw new InvalidOperationException("OpenAI returned no transcript.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var message))
            {
                throw new OpenAIException(
                    $"OpenAI error {(int)response.StatusCode}: {message.GetString() ?? response.ReasonPhrase}");
            }
        }
        catch (JsonException)
        {
        }

        throw new OpenAIException($"OpenAI error {(int)response.StatusCode}: {response.ReasonPhrase}.");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class OpenAIException(string message) : Exception(message);
