using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TmdbTeleBot.Handlers;

public static class CallbackHandlers
{
    public static async Task HandleCallbackQuery(ITelegramBotClient bot, Update update, string apiUrl, CancellationToken cancellationToken)
    {
        var callback = update.CallbackQuery;
        if (callback?.Message == null)
        {
            await bot.AnswerCallbackQuery(callback?.Id ?? "", text: "❌ Сталася помилка", cancellationToken: cancellationToken);
            return;
        }

        var data = callback.Data!;
        var chatId = callback.Message.Chat.Id;

        if (HandlerUtils.TryParseCallbackData(data, "movie_details", out var userId, out var movieId))
        {
            var json = await HandlerUtils.GetJsonAsync($"{apiUrl}/movies/{userId}/{movieId}");
            if (json is null) return;

            var message = HandlerUtils.FormatMovie(json.Value);

            await HandlerUtils.SendMovieWithOptionalPoster(bot, chatId, json.Value, message, cancellationToken);
        }
        else if (HandlerUtils.TryParseCallbackData(data, "movie_save", out userId, out movieId))
        {
            if (!Guid.TryParse(userId, out var userGuid) || !int.TryParse(movieId, out var movieInt))
            {
                await bot.SendMessage(chatId, "❌ Некоректні дані", cancellationToken: cancellationToken);
                return;
            }

            if (!MovieCache.TryGet(userGuid, movieInt, out var movieJson))
            {
                await bot.SendMessage(chatId, "⚠️ Не вдалося знайти фільм у кеші", cancellationToken: cancellationToken);
                return;
            }

            var rawJson = movieJson.GetRawText();

            var jsonContent = $"{rawJson}";

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await HandlerUtils.HttpClient.PostAsync($"{apiUrl}/movie/{userGuid}/save", content, cancellationToken);

            var resultMsg = response.IsSuccessStatusCode ? $"✅ Збережено до списку" : "❌ Не вдалося зберегти";
            await bot.SendMessage(chatId, resultMsg, cancellationToken: cancellationToken);
        }

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }
}
