using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Models;
using TelegramBot.Services;
using TelegramBot.Scenarios;
using System.Globalization;

namespace TelegramBot;

public sealed class UpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IUserService _userService;
    private readonly IToDoService _toDoService;
    private readonly IReadOnlyList<IScenario> _scenarios;
    private readonly IScenarioContextRepository _contextRepository;

    public UpdateHandler(ITelegramBotClient bot, IUserService userService, IToDoService toDoService,
        IEnumerable<IScenario> scenarios, IScenarioContextRepository contextRepository)
    {
        _bot = bot;
        _userService = userService;
        _toDoService = toDoService;
        _scenarios = scenarios.ToArray();
        _contextRepository = contextRepository;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (update.Message is not { From: { IsBot: false } sender } message ||
            message.Chat.Type != ChatType.Private) return;

        var text = message.Text?.Trim() ?? "";
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.FirstOrDefault()?.Split('@')[0].ToLowerInvariant() ?? "";
        var argument = parts.Length > 1 ? parts[1].Trim() : "";

        var context = await _contextRepository.GetContext(sender.Id, ct);
        if (command == "/cancel")
        {
            await _contextRepository.ResetContext(sender.Id, ct);
            await Reply(message.Chat.Id, context is null ? "Нет активного сценария." : "Текущий сценарий отменён.", ct);
            return;
        }
        if (context is not null)
        {
            await ProcessScenario(context, update, ct);
            return;
        }

        if (command == "/start")
        {
            await _userService.RegisterUser(sender.Id, sender.Username, ct);
            await Reply(message.Chat.Id, "Добро пожаловать в ToDo бот! Команды доступны на клавиатуре. Справка: /help.", ct);
            return;
        }
        if (command == "/help")
        {
            await Reply(message.Chat.Id, "/start - регистрация\n/addtask - создание задачи: название, затем срок dd.MM.yyyy\n/cancel - отмена текущего сценария\n/showtask или /showtasks - активные задачи\n/showalltasks - все задачи\n/completetask <id> - завершить\n/removetask <id> - удалить\n/report - статистика\n/help - справка\nВо время создания задачи введите запрошенное значение или /cancel.", ct);
            return;
        }

        var user = await _userService.GetUser(sender.Id, ct);
        if (user is null)
        {
            await Reply(message.Chat.Id, "Сначала зарегистрируйтесь командой /start.", ct);
            return;
        }

        switch (command)
        {
            case "/addtask":
                var newContext = new ScenarioContext(ScenarioType.AddTask) { UserId = sender.Id };
                await ProcessScenario(newContext, update, ct);
                break;
            case "/showtask":
            case "/showtasks":
                await ShowTasks(user, message.Chat.Id, onlyActive: true, ct);
                break;
            case "/showalltasks":
                await ShowTasks(user, message.Chat.Id, onlyActive: false, ct);
                break;
            case "/completetask":
            case "/removetask":
                if (!Guid.TryParse(argument, out var id))
                {
                    await Reply(message.Chat.Id, $"Формат: {command} <id>. Идентификатор возьмите из /showalltasks.", ct);
                    break;
                }
                var changed = command == "/completetask"
                    ? await _toDoService.Complete(user.UserId, id, ct)
                    : await _toDoService.Delete(user.UserId, id, ct);
                await Reply(message.Chat.Id, changed
                    ? command == "/completetask" ? "Задача завершена." : "Задача удалена."
                    : "Задача не найдена.", ct);
                break;
            case "/report":
                var tasks = await _toDoService.GetAllByUserId(user.UserId, ct);
                await Reply(message.Chat.Id, $"Всего: {tasks.Count}\nАктивных: {tasks.Count(x => x.State == ToDoItemState.Active)}\nЗавершённых: {tasks.Count(x => x.State == ToDoItemState.Completed)}", ct);
                break;
            default:
                await Reply(message.Chat.Id, "Выберите команду на клавиатуре или откройте /help.", ct);
                break;
        }
    }

    private async Task ShowTasks(ToDoUser user, long chatId, bool onlyActive, CancellationToken ct)
    {
        var tasks = await _toDoService.GetAllByUserId(user.UserId, ct);
        var filtered = tasks.Where(x => !onlyActive || x.State == ToDoItemState.Active).ToArray();
        if (filtered.Length == 0)
        {
            await Reply(chatId, "Список задач пуст.", ct);
            return;
        }
        foreach (var item in filtered)
            await Reply(chatId, $"{item.Name}\nСрок: {item.Deadline.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}\nСтатус: {(item.State == ToDoItemState.Active ? "Активна" : "Завершена")}\nID: {item.Id}", ct);
    }

    public IScenario GetScenario(ScenarioType scenario) =>
        _scenarios.FirstOrDefault(x => x.CanHandle(scenario))
        ?? throw new InvalidOperationException($"Обработчик сценария {scenario} не найден.");

    public async Task ProcessScenario(ScenarioContext context, Update update, CancellationToken ct)
    {
        var scenario = GetScenario(context.CurrentScenario);
        var result = await scenario.HandleMessageAsync(_bot, context, update, ct);
        if (result == ScenarioResult.Completed)
        {
            await _contextRepository.ResetContext(context.UserId, ct);
            await Reply(update.Message!.Chat.Id, "Выберите следующую команду:", ct);
        }
        else
        {
            await _contextRepository.SetContext(context.UserId, context, ct);
        }
    }

    private Task Reply(long chatId, string text, CancellationToken ct) =>
        _bot.SendMessage(chatId, text, replyMarkup: BotKeyboards.Main, cancellationToken: ct);
}
