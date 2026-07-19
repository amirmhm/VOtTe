using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

    public async Task<string> TransformTranscriptAsync(string transcript, TextStyleMode mode,
        string model, string apiKey, CancellationToken cancellationToken = default)
    {
        if (mode == TextStyleMode.Exact || string.IsNullOrWhiteSpace(transcript)) return transcript;

        var instructions = mode switch
        {
            TextStyleMode.Polished =>
                "Polish the dictated text. Preserve its meaning and language. Remove filler words and false starts, " +
                "and fix punctuation, capitalization, and obvious grammar. Do not add facts, commentary, quotation " +
                "marks, or labels. Return only the finished text.",
            TextStyleMode.Notes =>
                "Convert the dictated text into concise, readable bullet-point notes. Preserve every material fact " +
                "and the original language. Do not invent details or add a heading unless the speaker requested one. " +
                "Return only the notes.",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        var payload = new
        {
            model,
            reasoning = new { effort = "low" },
            text = new { verbosity = "low" },
            instructions,
            input = transcript,
            store = false,
            safety_identifier = CreateSafetyIdentifier()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var result = ReadOutputText(document.RootElement);
        if (!string.IsNullOrWhiteSpace(result)) return result.Trim();
        throw new InvalidOperationException("OpenAI returned no transformed text.");
    }

    private static string ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var pieces = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var itemType) || itemType.GetString() != "message" ||
                !item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var partType) || partType.GetString() != "output_text" ||
                    !part.TryGetProperty("text", out var text))
                    continue;
                var value = text.GetString();
                if (!string.IsNullOrEmpty(value)) pieces.Add(value);
            }
        }
        return string.Join(Environment.NewLine, pieces);
    }

    private static string CreateSafetyIdentifier()
    {
        var source = $"{Environment.UserDomainName}\\{Environment.UserName}|VoxPilot";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
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
