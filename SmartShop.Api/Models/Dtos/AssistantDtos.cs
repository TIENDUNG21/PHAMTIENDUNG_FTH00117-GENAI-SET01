using System.ComponentModel.DataAnnotations;

namespace SmartShop.Api.Models.Dtos;

public class ChatRequest
{
    [Required, MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
}
