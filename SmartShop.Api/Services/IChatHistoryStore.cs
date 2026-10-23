using SmartShop.Api.Services.Gemini;

namespace SmartShop.Api.Services;

/// <summary>Keeps the recent conversation turns for each assistant chat session.</summary>
public interface IChatHistoryStore
{
    IReadOnlyList<ChatTurn> GetHistory(string sessionId);
    void Append(string sessionId, ChatTurn turn);
}
