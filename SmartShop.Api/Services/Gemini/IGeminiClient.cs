namespace SmartShop.Api.Services.Gemini;

public interface IGeminiClient
{
    /// <summary>
    /// Calls the Gemini generateContent endpoint and returns the plain text of the first candidate.
    /// </summary>
    /// <param name="systemInstruction">Optional system instruction that sets the model's role/behavior.</param>
    /// <param name="contents">The conversation turns to send (at least one "user" turn).</param>
    /// <param name="config">Optional generation config (temperature, JSON mode, response schema, ...).</param>
    Task<string> GenerateContentAsync(
        string? systemInstruction,
        IReadOnlyList<ChatTurn> contents,
        GenerationConfig? config = null,
        CancellationToken cancellationToken = default);
}
