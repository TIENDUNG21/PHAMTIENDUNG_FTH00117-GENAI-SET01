using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SmartShop.Api.Services.Gemini;

namespace SmartShop.Api.Services;

/// <summary>
/// Stores chat history per sessionId in memory with a sliding expiration, so idle sessions
/// are evicted automatically. Suitable for a single-instance deployment; for multi-instance
/// hosting, back this with a distributed cache or a database table instead.
/// </summary>
public class InMemoryChatHistoryStore : IChatHistoryStore
{
    private const int MaxTurns = 20;
    private static readonly TimeSpan SessionExpiration = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public InMemoryChatHistoryStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public IReadOnlyList<ChatTurn> GetHistory(string sessionId)
    {
        return _cache.TryGetValue(sessionId, out List<ChatTurn>? history) && history is not null
            ? history.ToList()
            : Array.Empty<ChatTurn>();
    }

    public void Append(string sessionId, ChatTurn turn)
    {
        lock (_locks.GetOrAdd(sessionId, static _ => new object()))
        {
            var history = _cache.GetOrCreate(sessionId, entry =>
            {
                entry.SlidingExpiration = SessionExpiration;
                return new List<ChatTurn>();
            })!;

            history.Add(turn);
            if (history.Count > MaxTurns)
            {
                history.RemoveRange(0, history.Count - MaxTurns);
            }
        }
    }
}
