using System.Net.Http.Json;
using System.Text.Json;
using Telegram.Bot;

namespace TmdbTeleBot.Handlers;

public static class CommandHandlers
{
    private static async Task TryFetchUserId(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        try
        {
            var response = await HandlerUtils.HttpClient.PostAsJsonAsync($"{apiUrl}/user/save", new { chatId }, cancellationToken: cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonDocument.Parse(json).RootElement;

                if (root.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var userId))
                {
                    UserCache.SetUserId(chatId, userId);
                    Console.WriteLine($"✅ Користувача додано до локального кешу: {userId}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Не вдалося зберегти користувача. Код: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Помилка при отриманні userId: {ex.Message}");
        }
    }

    public static async Task HandleStart(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        var greeting = "🎬 Вітаю! Я бот для пошуку фільмів через TMDB.\n\n" +
                       "Ти можеш отримати випадковий фільм, переглянути популярні, збережені або статистику.\n";

        await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
        await bot.SendMessage(chatId, greeting, cancellationToken: cancellationToken);
    }

    public static async Task HandleRandom(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movie/random");
        if (json is null)
        {
            await bot.SendMessage(chatId, "⚠️ Не вдалося отримати фільм", cancellationToken: cancellationToken);
            return;
        }

        var message = HandlerUtils.FormatMovie(json.Value);
        await HandlerUtils.SendMovieWithOptionalPoster(bot, chatId, json.Value, message, cancellationToken);
    }

    public static async Task HandleSearchCommand(ITelegramBotClient bot, long chatId, string apiUrl, CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            Console.WriteLine($"ℹ️ userId відсутній, виконуємо /start автоматично для chatId={chatId}");
            await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
        }

        await bot.SendMessage(chatId, "✏️ Введіть назву фільму для пошуку:", cancellationToken: cancellationToken);
    }


    public static async Task HandleTextInput(ITelegramBotClient bot, long chatId, string userInput, string apiUrl, CancellationToken cancellationToken)
    {
        var userId = UserCache.GetUserId(chatId);
        if (userId == null)
        {
            Console.WriteLine($"ℹ️ userId відсутній, виконуємо авто-створення для chatId={chatId}");
            await TryFetchUserId(bot, chatId, apiUrl, cancellationToken);
            userId = UserCache.GetUserId(chatId);

            if (userId == null)
            {
                await bot.SendMessage(chatId, "❗ Не вдалося створити користувача. Спробуйте ще раз пізніше.", cancellationToken: cancellationToken);
                return;
            }
        }

        var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/search/{userId}?query={Uri.EscapeDataString(userInput)}");

        if (json is null)
        {
            await bot.SendMessage(chatId, "⚠️ Помилка під час пошуку фільму. Спробуйте пізніше.", cancellationToken: cancellationToken);
            return;
        }

        var results = json.Value.EnumerateArray().ToList();
        if (!results.Any())
        {
            await bot.SendMessage(chatId, "🤷‍♂️ Нічого не знайдено за вашим запитом.", cancellationToken: cancellationToken);
            return;
        }

        foreach (var movie in results)
        {
            var message = HandlerUtils.FormatMovie(movie);
            var movieId = movie.GetProperty("id").GetInt32();
            var keyboard = HandlerUtils.CreateMovieButtons(userId.Value, movieId);

            MovieCache.Set(userId.Value, movieId, movie);

            await HandlerUtils.SendMovieWithOptionalPoster(bot, chatId, movie, message, cancellationToken, keyboard);
        }
    }
}