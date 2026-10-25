using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBot.Scenarios;

public interface IScenario
{
    bool CanHandle(ScenarioType scenario);
    Task<ScenarioResult> HandleMessageAsync(ITelegramBotClient bot, ScenarioContext context,
        Update update, CancellationToken ct);
}
