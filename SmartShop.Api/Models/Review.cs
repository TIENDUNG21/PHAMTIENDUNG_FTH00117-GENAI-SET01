using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartShop.Api.Models;

public class Review
{
    [Key]
    public int ReviewId { get; set; }

    [ForeignKey(nameof(Product))]
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    [Required, MaxLength(64)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public int Rating { get; set; }

    [MaxLength(16)]
    public string? Sentiment { get; set; }

    public double? Score { get; set; }

    [MaxLength(256)]
    public string? Summary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
