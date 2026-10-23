using System.Net;

namespace SmartShop.Api.Services.Gemini;

/// <summary>Raised when the Gemini REST API returns a non-success response or an unparsable payload.</summary>
public class GeminiApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public bool IsQuotaExceeded => StatusCode == HttpStatusCode.TooManyRequests;

    public GeminiApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>Raised when Gemini's response text could not be parsed into the expected JSON shape.</summary>
public class GeminiInvalidJsonException : Exception
{
    public string RawResponse { get; }

    public GeminiInvalidJsonException(string rawResponse, Exception inner)
        : base("Gemini returned a response that could not be parsed as the expected JSON structure.", inner)
    {
        RawResponse = rawResponse;
    }
}
