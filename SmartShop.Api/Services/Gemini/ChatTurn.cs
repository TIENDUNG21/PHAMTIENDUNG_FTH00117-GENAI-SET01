namespace SmartShop.Api.Services.Gemini;

/// <summary>One turn of a Gemini conversation. Role is "user" or "model".</summary>
public record ChatTurn(string Role, string Text)
{
    public static ChatTurn User(string text) => new("user", text);
    public static ChatTurn Model(string text) => new("model", text);
}

public class GenerationConfig
{
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }

    /// <summary>Set to "application/json" to force JSON mode.</summary>
    public string? ResponseMimeType { get; set; }

    /// <summary>JSON Schema object describing the expected response shape (used with JSON mode).</summary>
    public object? ResponseSchema { get; set; }
}
