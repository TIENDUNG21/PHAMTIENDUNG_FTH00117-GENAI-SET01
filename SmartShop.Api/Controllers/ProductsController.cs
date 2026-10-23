using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Data;
using SmartShop.Api.Models.Dtos;
using SmartShop.Api.Services.Gemini;
using SmartShop.Api.Services.Prompts;

namespace SmartShop.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly SmartShopDbContext _db;
    private readonly IGeminiClient _gemini;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(SmartShopDbContext db, IGeminiClient gemini, ILogger<ProductsController> logger)
    {
        _db = db;
        _gemini = gemini;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll(CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.ProductId).ToListAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>
    /// Requirement 1: generates an at-most-80-word marketing description for a product using
    /// Gemini (system instruction = "act as a professional copywriter"), persists it into the
    /// Description column, and returns it.
    /// </summary>
    [HttpPost("{id:int}/generatedescription")]
    public async Task<ActionResult<GenerateDescriptionResponse>> GenerateDescription(int id, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id, cancellationToken);
        if (product is null)
        {
            return NotFound(new { message = $"Product {id} was not found." });
        }

        var userPrompt = PromptTemplates.BuildDescriptionUserPrompt(product);

        var description = await _gemini.GenerateContentAsync(
            systemInstruction: PromptTemplates.DescriptionSystemInstruction,
            contents: new[] { ChatTurn.User(userPrompt) },
            config: new GenerationConfig { Temperature = 0.7, MaxOutputTokens = 300 },
            cancellationToken: cancellationToken);

        product.Description = description;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Generated description for product {ProductId}.", id);

        return Ok(new GenerateDescriptionResponse
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            Description = description
        });
    }
}
