using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot.Models;
using TelegramBot.Services;

namespace TelegramBot.Scenarios;

public sealed class AddTaskScenario : IScenario
{
    private const string UserKey = "User";
    private const string NameKey = "Name";
    private const string CreatedTaskKey = "CreatedTask";
    private readonly IUserService _userService;
    private readonly IToDoService _toDoService;

    public AddTaskScenario(IUserService userService, IToDoService toDoService)
    {
        _userService = userService;
        _toDoService = toDoService;
    }

    public bool CanHandle(ScenarioType scenario) => scenario == ScenarioType.AddTask;

    public async Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot,
        ScenarioContext context, Update update, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var message = update.Message ?? throw new ArgumentException("Ожидается сообщение.", nameof(update));
        switch (context.CurrentStep)
        {
            case null:
                var user = await _userService.GetUser(context.UserId, ct);
                if (user is null)
                {
                    await bot.SendMessage(message.Chat.Id, "Сначала зарегистрируйтесь командой /start.",
                        cancellationToken: ct);
                    return ScenarioResult.Completed;
                }
                context.Data[UserKey] = user;
                await Prompt(bot, message.Chat.Id, "Введите название задачи:", ct);
                context.CurrentStep = "Name";
                return ScenarioResult.Transition;

            case "Name":
                var name = message.Text?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > ToDoService.MaxNameLength || name.StartsWith('/'))
                {
                    await Prompt(bot, message.Chat.Id,
                        "Введите название задачи текстом от 1 до 128 символов. Для отмены используйте /cancel.", ct);
                    return ScenarioResult.Transition;
                }
                await Prompt(bot, message.Chat.Id, "Введите срок выполнения (dd.MM.yyyy):", ct);
                context.Data[NameKey] = name;
                context.CurrentStep = "Deadline";
                return ScenarioResult.Transition;

            case "Deadline":
                var dateText = message.Text;
                if (dateText is null || dateText.Length != 10 ||
                    !DateTime.TryParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var deadline))
                {
                    await Prompt(bot, message.Chat.Id,
                        "Неверный формат даты. Введите существующую дату в формате dd.MM.yyyy:", ct);
                    return ScenarioResult.Transition;
                }
                // Сохраняем созданную задачу, чтобы повтор отправки ответа не создал её второй раз.
                if (!context.Data.TryGetValue(CreatedTaskKey, out var savedTask))
                {
                    savedTask = await _toDoService.Add((ToDoUser)context.Data[UserKey],
                        (string)context.Data[NameKey], deadline, ct);
                    context.Data[CreatedTaskKey] = savedTask;
                }
                var item = (ToDoItem)savedTask;
                await bot.SendMessage(message.Chat.Id,
                    $"Задача {item.Name} успешно добавлена.\nСрок: {item.Deadline.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}",
                    cancellationToken: ct);
                return ScenarioResult.Completed;

            default:
                throw new InvalidOperationException($"Неизвестный шаг сценария: {context.CurrentStep}.");
        }
    }

    private static Task Prompt(ITelegramBotClient bot, long chatId, string text, CancellationToken ct) =>
        bot.SendMessage(chatId, text, replyMarkup: BotKeyboards.Cancel, cancellationToken: ct);
}
