using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TmdbTeleBot.Handlers;

public static class HandlerUtils
{
    public static readonly HttpClient HttpClient = new();

    public static async Task<JsonElement?> GetJsonAsync(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content).RootElement;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HTTP error: {ex.Message}");
            return null;
        }
    }

    public static async Task<string?> GetRawJsonAsync(string url)
    {
        try
        {
            return await HttpClient.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ HTTP raw error: {ex.Message}");
            return null;
        }
    }

    public static string FormatMovie(JsonElement movie)
    {
        var title = movie.GetProperty("title").GetString();
        var releaseDate = DateTime.TryParse(movie.GetProperty("release_date").GetString(), out var dt) ? dt.Year : 0;
        var rating = movie.GetProperty("vote_average").GetDecimal();
        var overview = movie.GetProperty("overview").GetString();

        return $"🎬 <b>{title}</b>\n📅 Рік: {releaseDate}\n⭐ Рейтинг: {rating}\n📝 Опис: {overview}";
    }

    public static async Task SendMovieWithOptionalPoster(
        ITelegramBotClient bot,
        long chatId,
        JsonElement movieJson,
        string message,
        CancellationToken cancellationToken,
        ReplyMarkup? replyMarkup = null
        )
    {
        var posterPath = movieJson.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null;

        if (!string.IsNullOrEmpty(posterPath))
        {
            var posterUrl = $"https://image.tmdb.org/t/p/w500{posterPath}";
            await bot.SendPhoto(
                chatId,
                InputFile.FromUri(posterUrl),
                caption: message,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        else
        {
            await bot.SendMessage(
                chatId,
                message,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
    }

    public static InlineKeyboardMarkup CreateMovieButtons(Guid userId, int movieId)
    {
        return new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("ℹ️ Докладніше", $"movie_details:{userId}:{movieId}"),
                InlineKeyboardButton.WithCallbackData("💾 Зберегти", $"movie_save:{userId}:{movieId}")
            ]
        ]);
    }

    public static bool TryParseCallbackData(string data, string prefix, out string userId, out string movieId)
    {
        userId = movieId = string.Empty;
        var parts = data.Split(':');
        if (parts.Length != 3 || parts[0] != prefix) return false;
        userId = parts[1];
        movieId = parts[2];
        return true;
    }
} 
