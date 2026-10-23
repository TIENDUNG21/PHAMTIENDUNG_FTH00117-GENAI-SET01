using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Data;
using SmartShop.Api.Models.Dtos;

namespace SmartShop.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly SmartShopDbContext _db;

    public ReportsController(SmartShopDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Requirement 4: aggregates the Review table per product (total reviews, sentiment
    /// breakdown, average star rating). Products with no reviews are still listed with zeros.
    /// </summary>
    [HttpGet("reviews")]
    public async Task<ActionResult<List<ReviewReportRow>>> GetReviewReport(CancellationToken cancellationToken)
    {
        var raw = await _db.Products
            .AsNoTracking()
            .OrderBy(p => p.ProductId)
            .Select(p => new
            {
                p.ProductName,
                Total = p.Reviews.Count,
                Positive = p.Reviews.Count(r => r.Sentiment == "Positive"),
                Negative = p.Reviews.Count(r => r.Sentiment == "Negative"),
                Neutral = p.Reviews.Count(r => r.Sentiment == "Neutral"),
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0.0
            })
            .ToListAsync(cancellationToken);

        var rows = raw.Select((r, index) => new ReviewReportRow
        {
            No = index + 1,
            ProductName = r.ProductName,
            TotalReviews = r.Total,
            Positive = r.Positive,
            Negative = r.Negative,
            Neutral = r.Neutral,
            AverageRating = Math.Round(r.AverageRating, 1)
        }).ToList();

        return Ok(rows);
    }
}
