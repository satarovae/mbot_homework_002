using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramBot;
using TelegramBot.Services;
using TelegramBot.Scenarios;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var token = "8852045176:AAGvvjcrhPDHFnBnxddxNYV3wUjl0VIXAK4";
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Задайте переменную окружения TELEGRAM_BOT_TOKEN с токеном от @BotFather.");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; cts.Cancel(); };

try
{
    var bot = new TelegramBotClient(token);
    var userService = new UserService();
    var toDoService = new ToDoService();
    var contextRepository = new InMemoryScenarioContextRepository();
    IScenario[] scenarios = { new AddTaskScenario(userService, toDoService) };
    var handler = new UpdateHandler(bot, userService, toDoService, scenarios, contextRepository);
    var me = await bot.GetMe(cts.Token);
    Console.WriteLine($"Бот @{me.Username} запущен. Остановка: Ctrl+C.");
    await bot.ReceiveAsync(handler.HandleUpdateAsync,
        async (_, _, ct) =>
        {
            Console.Error.WriteLine("Ошибка при получении или обработке сообщения. Повтор через 3 секунды.");
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        },
        new ReceiverOptions { AllowedUpdates = new[] { UpdateType.Message } }, cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
catch (Exception ex)
{
    Console.Error.WriteLine($"Запуск не выполнен ({ex.GetType().Name}). Проверьте токен и доступ к Telegram.");
    return 1;
}

return 0;
