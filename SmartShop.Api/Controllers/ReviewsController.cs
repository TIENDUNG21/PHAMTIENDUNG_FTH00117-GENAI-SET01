using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Data;
using SmartShop.Api.Models;
using SmartShop.Api.Models.Dtos;
using SmartShop.Api.Services.Gemini;
using SmartShop.Api.Services.Prompts;

namespace SmartShop.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private static readonly string[] AllowedSentiments = { "Positive", "Negative", "Neutral" };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SmartShopDbContext _db;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(SmartShopDbContext db, IGeminiClient gemini, ILogger<ReviewsController> logger)
    {
        _db = db;
        _gemini = gemini;
        _logger = logger;
    }

    /// <summary>
    /// Requirement 2: sends the review to Gemini in JSON mode (response schema), deserializes the
    /// result into <see cref="GeminiReviewAnalysis"/> and persists the review together with the
    /// AI-generated sentiment, score and summary.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalyzeReviewResponse>> Analyze(AnalyzeReviewRequest request, CancellationToken cancellationToken)
    {
        var productExists = await _db.Products.AnyAsync(p => p.ProductId == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return NotFound(new { message = $"Product {request.ProductId} was not found." });
        }

        var userPrompt = PromptTemplates.BuildReviewAnalysisUserPrompt(request.Rating, request.Content);

        var jsonText = await _gemini.GenerateContentAsync(
            systemInstruction: PromptTemplates.ReviewAnalysisSystemInstruction,
            contents: new[] { ChatTurn.User(userPrompt) },
            config: new GenerationConfig
            {
                Temperature = 0.2,
                MaxOutputTokens = 300,
                ResponseMimeType = "application/json",
                ResponseSchema = PromptTemplates.ReviewAnalysisResponseSchema
            },
            cancellationToken: cancellationToken);

        var analysis = ParseAnalysis(jsonText);

        var review = new Review
        {
            ProductId = request.ProductId,
            CustomerName = request.CustomerName,
            Content = request.Content,
            Rating = request.Rating,
            Sentiment = analysis.Sentiment,
            Score = Math.Clamp(analysis.Score, 0.0, 1.0),
            Summary = analysis.Summary,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved review {ReviewId} for product {ProductId} with sentiment {Sentiment}.",
            review.ReviewId, review.ProductId, review.Sentiment);

        return CreatedAtAction(nameof(Analyze), new { id = review.ReviewId }, new AnalyzeReviewResponse
        {
            ReviewId = review.ReviewId,
            ProductId = review.ProductId,
            CustomerName = review.CustomerName,
            Content = review.Content,
            Rating = review.Rating,
            Sentiment = review.Sentiment!,
            Score = review.Score!.Value,
            Summary = review.Summary!,
            CreatedAt = review.CreatedAt
        });
    }

    private GeminiReviewAnalysis ParseAnalysis(string jsonText)
    {
        GeminiReviewAnalysis? analysis;
        try
        {
            analysis = JsonSerializer.Deserialize<GeminiReviewAnalysis>(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not deserialize Gemini review analysis. Raw text: {RawText}", jsonText);
            throw new GeminiInvalidJsonException(jsonText, ex);
        }

        if (analysis is null || string.IsNullOrWhiteSpace(analysis.Sentiment) ||
            !AllowedSentiments.Contains(analysis.Sentiment, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogError("Gemini review analysis failed schema validation. Raw text: {RawText}", jsonText);
            throw new GeminiInvalidJsonException(jsonText, new InvalidOperationException("Sentiment missing or not one of Positive/Negative/Neutral."));
        }

        return analysis;
    }
}
