using System.Net;
using Microsoft.AspNetCore.Mvc;
using SmartShop.Api.Services.Gemini;

namespace SmartShop.Api.Middleware;

/// <summary>
/// Centralizes error handling for Gemini calls (quota/429, upstream failures, unparsable JSON)
/// and unexpected exceptions, so every controller returns a consistent ProblemDetails payload
/// instead of a raw 500 or an unhandled crash.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (GeminiApiException ex) when (ex.IsQuotaExceeded)
        {
            _logger.LogWarning(ex, "Gemini quota exceeded.");
            await WriteProblemAsync(context, (int)HttpStatusCode.TooManyRequests,
                "Gemini API quota exceeded", "The Gemini API rate limit or quota was exceeded. Please retry after a short delay.");
        }
        catch (GeminiApiException ex)
        {
            _logger.LogError(ex, "Gemini API call failed.");
            await WriteProblemAsync(context, (int)HttpStatusCode.BadGateway,
                "Gemini API error", ex.Message);
        }
        catch (GeminiInvalidJsonException ex)
        {
            _logger.LogError(ex, "Gemini returned a response that could not be parsed as JSON.");
            await WriteProblemAsync(context, (int)HttpStatusCode.BadGateway,
                "Invalid response from Gemini",
                "Gemini did not return a parsable JSON response. The raw response has been logged for diagnostics.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Service misconfiguration.");
            await WriteProblemAsync(context, (int)HttpStatusCode.InternalServerError,
                "Service misconfigured", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, (int)HttpStatusCode.InternalServerError,
                "Unexpected error", "An unexpected error occurred while processing the request.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
