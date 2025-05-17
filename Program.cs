using TmdbTeleBot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var botClient = new TelegramBotClient("7902299486:AAEZ6y9SB0KKhqUpeLLWID2-sRI2Gk8iOf0");

var cts = new CancellationTokenSource();

botClient.StartReceiving(
    HandleUpdate,
    HandleError,
    new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
    cancellationToken: cts.Token
);

await botClient.SendMessage(
    chatId: 952857686,
    text: "✅ Бот запущено та готовий до роботи",
    cancellationToken: cts.Token
);

Console.WriteLine("Bot is running...");
Console.ReadLine();

async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.CallbackQuery)
    {
        await Handlers.HandleCallbackQuery(bot, update, cancellationToken);
        return;
    }

    if (update.Message is { Text: { } messageText })
    {
        var chatId = update.Message.Chat.Id;

        switch (messageText)
        {
            case "/start":
                await Handlers.HandleStart(bot, chatId, cancellationToken);
                break;
            case "/menu":
                await Handlers.ShowMenu(bot, chatId, cancellationToken);
                break;
            case "/random":
                await Handlers.HandleRandom(bot, chatId, cancellationToken);
                break;
            case "/search":
                SearchState.Set(chatId);
                await Handlers.HandleSearchCommand(bot, update, cancellationToken);
                break;
            default:
                if (SearchState.Is(chatId))
                {
                    SearchState.Clear(chatId);
                    await Handlers.HandleTextInput(bot, update, cancellationToken);
                }
                break;
        }
    }
}

Task HandleError(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Error: {exception.Message}");
    return Task.CompletedTask;
}
