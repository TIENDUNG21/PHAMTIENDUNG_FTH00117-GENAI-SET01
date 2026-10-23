using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Data;
using SmartShop.Api.Models.Dtos;
using SmartShop.Api.Services;
using SmartShop.Api.Services.Gemini;
using SmartShop.Api.Services.Prompts;

namespace SmartShop.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly SmartShopDbContext _db;
    private readonly IGeminiClient _gemini;
    private readonly IChatHistoryStore _history;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(
        SmartShopDbContext db,
        IGeminiClient gemini,
        IChatHistoryStore history,
        ILogger<AssistantController> logger)
    {
        _db = db;
        _gemini = gemini;
        _history = history;
        _logger = logger;
    }

    /// <summary>
    /// Requirement 3: a shopping assistant that only answers questions about products in the
    /// Product table. The current catalog is loaded from the database on every call and injected
    /// as context via the system instruction, and the conversation history is kept per sessionId
    /// so follow-up questions (e.g. "How much does it cost?") resolve against earlier turns.
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(ChatRequest request, CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking().OrderBy(p => p.ProductId).ToListAsync(cancellationToken);
        var systemInstruction = PromptTemplates.BuildAssistantSystemInstruction(products);

        var priorTurns = _history.GetHistory(request.SessionId);
        var contents = priorTurns.Append(ChatTurn.User(request.Message)).ToList();

        var reply = await _gemini.GenerateContentAsync(
            systemInstruction: systemInstruction,
            contents: contents,
            config: new GenerationConfig { Temperature = 0.4, MaxOutputTokens = 400 },
            cancellationToken: cancellationToken);

        _history.Append(request.SessionId, ChatTurn.User(request.Message));
        _history.Append(request.SessionId, ChatTurn.Model(reply));

        _logger.LogInformation("Assistant replied for session {SessionId}.", request.SessionId);

        return Ok(new ChatResponse
        {
            SessionId = request.SessionId,
            Reply = reply
        });
    }
}
