using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SmartShop.Api.Services.Gemini;

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateContentAsync(
        string? systemInstruction,
        IReadOnlyList<ChatTurn> contents,
        GenerationConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured. Set it via 'dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\"' " +
                "in development, or the GEMINI_API_KEY / Gemini__ApiKey environment variable in other environments.");
        }

        if (contents.Count == 0)
        {
            throw new ArgumentException("At least one chat turn is required.", nameof(contents));
        }

        var request = new GeminiRequest
        {
            SystemInstruction = systemInstruction is null
                ? null
                : new GeminiContent { Parts = { new GeminiPart { Text = systemInstruction } } },
            Contents = contents
                .Select(turn => new GeminiContent
                {
                    Role = turn.Role,
                    Parts = { new GeminiPart { Text = turn.Text } }
                })
                .ToList(),
            GenerationConfig = config is null
                ? null
                : new GeminiGenerationConfig
                {
                    Temperature = config.Temperature,
                    MaxOutputTokens = config.MaxOutputTokens,
                    ResponseMimeType = config.ResponseMimeType,
                    ResponseSchema = config.ResponseSchema
                }
        };

        var requestUri = $"models/{_options.Model}:generateContent";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        // The key is sent as a header (not a query-string parameter) to keep it out of
        // server access logs and browser/proxy history.
        httpRequest.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            ThrowForErrorResponse(httpResponse, body);
        }

        GeminiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiResponse>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Gemini returned a response body that is not valid JSON: {Body}", body);
            throw new GeminiInvalidJsonException(body, ex);
        }

        if (parsed?.PromptFeedback?.BlockReason is { Length: > 0 } blockReason)
        {
            throw new GeminiApiException(HttpStatusCode.UnprocessableEntity, $"Gemini blocked the prompt: {blockReason}");
        }

        var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini returned no candidate text. Raw response: {Body}", body);
            throw new GeminiApiException(HttpStatusCode.BadGateway, "Gemini returned an empty response.");
        }

        return text.Trim();
    }

    private void ThrowForErrorResponse(HttpResponseMessage httpResponse, string body)
    {
        string message = $"Gemini API request failed with status {(int)httpResponse.StatusCode}.";
        try
        {
            var envelope = JsonSerializer.Deserialize<GeminiErrorEnvelope>(body, JsonOptions);
            if (envelope?.Error is not null)
            {
                message = envelope.Error.Message;
            }
        }
        catch (JsonException)
        {
            // Fall back to the generic message above; the raw body is still logged below.
        }

        _logger.LogError(
            "Gemini API error {StatusCode}: {Message}. Raw body: {Body}",
            httpResponse.StatusCode, message, body);

        throw new GeminiApiException(httpResponse.StatusCode, message);
    }
}
