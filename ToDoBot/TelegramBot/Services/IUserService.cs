using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IUserService
{
    Task<ToDoUser> RegisterUser(long telegramUserId, string? userName, CancellationToken ct);
    Task<ToDoUser?> GetUser(long telegramUserId, CancellationToken ct);
}
