using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TmdbTeleBot;
using TmdbTeleBot.Config;
using TmdbTeleBot.Handlers;

var settings = LoadSettings();
var botClient = new TelegramBotClient(settings.Telegram.Token);
var cts = new CancellationTokenSource();

await StartBot(botClient, settings, cts.Token);
Console.ReadLine();

AppSettings LoadSettings()
{
    var configJson = File.ReadAllText("appsettings.json");
    return JsonSerializer.Deserialize<AppSettings>(configJson) ?? throw new Exception("❌ Не вдалося завантажити конфігурацію.");
}

async Task StartBot(TelegramBotClient client, AppSettings appSettings, CancellationToken cancellationToken)
{
    await client.DeleteWebhook(cancellationToken: cancellationToken);

    client.StartReceiving(HandleUpdate, HandleError, new ReceiverOptions { AllowedUpdates = [] }, cancellationToken: cancellationToken);

    await client.SendMessage(chatId: appSettings.Telegram.AdminChatId, text: "✅ Бот запущено та готовий до роботи", cancellationToken: cancellationToken);

    Console.WriteLine("Bot is running...");
}

async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
{
    var apiUrl = settings.Backend.ApiUrl;

    if (update.Type == UpdateType.CallbackQuery)
    {
        await CallbackHandlers.HandleCallbackQuery(bot, update, apiUrl, cancellationToken);
        return;
    }

    if (update.Message is { Text: { } messageText })
    {
        var chatId = update.Message.Chat.Id;

        if (await HandleCommand(bot, messageText, chatId, apiUrl, cancellationToken))
            return;

        if (SearchState.Is(chatId))
        {
            SearchState.Clear(chatId);
            await CommandHandlers.HandleTextInput(bot, chatId, messageText, apiUrl, cancellationToken);
        }
    }
}

static async Task<bool> HandleCommand(ITelegramBotClient bot, string messageText, long chatId, string apiUrl, CancellationToken cancellationToken)
{
    switch (messageText)
    {
        case "/start":
            await CommandHandlers.HandleStart(bot, chatId, apiUrl, cancellationToken);
            return true;

        case "/random":
            await CommandHandlers.HandleRandom(bot, chatId, apiUrl, cancellationToken);
            return true;

        case "/search":
            SearchState.Set(chatId);
            await CommandHandlers.HandleSearchCommand(bot, chatId, apiUrl, cancellationToken: cancellationToken);
            return true;

        default:
            return false;
    }
}

Task HandleError(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Error: {exception.Message}");
    return Task.CompletedTask;
}
