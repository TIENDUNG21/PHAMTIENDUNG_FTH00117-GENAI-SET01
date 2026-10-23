using System.ComponentModel.DataAnnotations;

namespace SmartShop.Api.Models.Dtos;

public class AnalyzeReviewRequest
{
    [Required]
    public int ProductId { get; set; }

    [Required, MaxLength(64)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }
}

/// <summary>JSON shape returned by Gemini for review analysis (response schema / JSON mode).</summary>
public class GeminiReviewAnalysis
{
    public string Sentiment { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class AnalyzeReviewResponse
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Sentiment { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
