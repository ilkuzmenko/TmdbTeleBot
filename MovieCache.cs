using System.Text.Json;

namespace TmdbTeleBot;

public static class MovieCache
{
    private static readonly Dictionary<string, JsonElement> _cache = new();

    public static void Set(Guid userId, int movieId, JsonElement json)
    {
        var key = $"{userId}:{movieId}";
        _cache[key] = json;
    }

    public static bool TryGet(Guid userId, int movieId, out JsonElement json)
    {
        var key = $"{userId}:{movieId}";
        return _cache.TryGetValue(key, out json);
    }
}
