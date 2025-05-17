using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TmdbTeleBot;

public static class Handlers
{
    public static async Task ShowMenu(ITelegramBotClient bot, long chatId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"ShowMenu: chatId={chatId}");

        var menu = "📋 Головне меню:\n\n" +
                   "/stats — статистика користувача\n" +
                   "/top — топ фільмів\n" +
                   "/random — випадковий фільм\n" +
                   "/saved — збережені фільми\n" +
                   "/search — пошук фільмів";

        await bot.SendMessage(chatId, menu, cancellationToken: cancellationToken);
    }

    public static async Task HandleStart(ITelegramBotClient bot, long chatId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"HandleStart: chatId={chatId}");

        var greeting = "🎬 Вітаю! Я бот для пошуку фільмів через TMDB.\n\n" +
                       "Ти можеш отримати випадковий фільм, переглянути популярні, збережені або статистику.\n";

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsJsonAsync("http://localhost:5180/user/save", new { chatId });
        var responseContent = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var userId))
            {
                UserCache.SetUserId(chatId, userId);
                Console.WriteLine($"✅ Збережено користувача: {userId}");
            }
            else
            {
                Console.WriteLine($"⚠️ Відповідь не містить коректного userId: {responseContent}");
            }
        }
        catch (JsonException)
        {
            Console.WriteLine($"❌ JSON parsing error: {responseContent}");
        }

        await bot.SendMessage(chatId, greeting, cancellationToken: cancellationToken);
        await ShowMenu(bot, chatId, cancellationToken);
    }

    public static async Task HandleRandom(ITelegramBotClient bot, long chatId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"HandleRandom: chatId={chatId}");

        using var httpClient = new HttpClient();
        string message = "⚠️ Не вдалося отримати фільм";

        try
        {
            var response = await httpClient.GetAsync("http://localhost:5180/movie/random");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var title = root.GetProperty("title").GetString();
                var releaseDate = root.GetProperty("release_date").GetString();
                var overview = root.GetProperty("overview").GetString();
                var rating = root.GetProperty("vote_average").GetDecimal();

                message = $"🎬 <b>{title}</b>\n" +
                          $"📅 Рік: {DateTime.Parse(releaseDate).Year}\n" +
                          $"⭐ Рейтинг: {rating}\n" +
                          $"📝 Опис: {overview}";
            }
            else
            {
                Console.WriteLine($"❌ Помилка отримання фільму: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Виняток при отриманні фільму: {ex.Message}");
        }

        await bot.SendMessage(
            chatId: chatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            cancellationToken: cancellationToken
        );

        await ShowMenu(bot, chatId, cancellationToken);
    }
    
    public static async Task HandleSearchCommand(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id;

        await bot.SendMessage(
            chatId,
            "✏️ Введіть назву фільму для пошуку:",
            cancellationToken: cancellationToken
        );
    }
    
    public static async Task HandleTextInput(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id;
        var userInput = update.Message.Text;

        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            await bot.SendMessage(chatId, "❗ Спочатку виконай /start", cancellationToken: cancellationToken);
            return;
        }

        using var httpClient = new HttpClient();
        var url = $"http://localhost:5180/search/{userId}?query={Uri.EscapeDataString(userInput)}";
        var response = await httpClient.GetStringAsync(url);

        var json = JsonDocument.Parse(response);
        var movies = json.RootElement.GetProperty("results");

        foreach (var movie in movies.EnumerateArray())
        {
            var id = movie.GetProperty("id").GetInt32();
            var title = movie.GetProperty("title").GetString();
            var rating = movie.GetProperty("vote_average").GetDecimal();
            var year = DateTime.Parse(movie.GetProperty("release_date").GetString() ?? "0001-01-01").Year;
            var overview = movie.GetProperty("overview").GetString();

            var message = $"🎬 <b>{title}</b> ({year})\n⭐ {rating}\n📝 {overview}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ℹ️ Докладніше", $"movie_details:{userId}:{id}"),
                    InlineKeyboardButton.WithCallbackData("💾 Зберегти", $"movie_save:{userId}:{id}")
                }
            });

            await bot.SendMessage(
                chatId,
                message,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );
        }
    }

    public static async Task HandleCallbackQuery(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        var callback = update.CallbackQuery!;
        var data = callback.Data!;
        var chatId = callback.Message.Chat.Id;

        if (data.StartsWith("movie_details:"))
        {
            var parts = data.Split(':');
            var userId = parts[1];
            var movieId = parts[2];

            var url = $"http://localhost:5180/movies/{userId}/{movieId}";
            var json = await new HttpClient().GetStringAsync(url);
            var movie = JsonDocument.Parse(json).RootElement;

            var msg = $"🎬 <b>{movie.GetProperty("title").GetString()}</b>\n" +
                      $"📅 {movie.GetProperty("release_date").GetString()}\n" +
                      $"⭐ {movie.GetProperty("vote_average").GetDecimal()}\n" +
                      $"📝 {movie.GetProperty("overview").GetString()}";

            await bot.SendMessage(chatId, msg, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
        }
        else if (data.StartsWith("movie_save:"))
        {
            var parts = data.Split(':');
            var userId = parts[1];
            var movieId = parts[2];

            var url = $"http://localhost:5180/movies/{userId}/{movieId}";
            var json = await new HttpClient().GetStringAsync(url);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var saveUrl = $"http://localhost:5180/movie/{userId}/save";
            var saveResp = await new HttpClient().PostAsync(saveUrl, content);

            var resultMsg = saveResp.IsSuccessStatusCode ? "✅ Збережено до списку" : "❌ Не вдалося зберегти";
            await bot.SendMessage(chatId, resultMsg, cancellationToken: cancellationToken);
        }

        await bot.AnswerCallbackQuery(
            callbackQueryId: callback.Id,
            cancellationToken: cancellationToken
        );
    }
}
