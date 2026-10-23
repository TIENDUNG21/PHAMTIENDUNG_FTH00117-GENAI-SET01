using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartShop.Api.Models;

public class Product
{
    [Key]
    public int ProductId { get; set; }

    [Required, MaxLength(128)]
    public string ProductName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required, MaxLength(512)]
    public string Features { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
