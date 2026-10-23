namespace SmartShop.Api.Models.Dtos;

public class GenerateDescriptionResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
