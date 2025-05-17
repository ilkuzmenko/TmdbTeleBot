namespace TmdbTeleBot;

public static class SearchState
{
    private static readonly HashSet<long> WaitingForSearch = new();

    public static void Set(long chatId) => WaitingForSearch.Add(chatId);
    public static bool Is(long chatId) => WaitingForSearch.Contains(chatId);
    public static void Clear(long chatId) => WaitingForSearch.Remove(chatId);
}