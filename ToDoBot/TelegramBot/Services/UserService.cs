using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class UserService : IUserService
{
    private readonly Dictionary<long, ToDoUser> _users = new();
    private readonly object _sync = new();

    public Task<ToDoUser> RegisterUser(long telegramUserId, string? userName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_users.TryGetValue(telegramUserId, out var user))
            {
                user = new ToDoUser { TelegramUserId = telegramUserId };
                _users.Add(telegramUserId, user);
            }
            user.TelegramUserName = userName;
            return Task.FromResult(user);
        }
    }

    public Task<ToDoUser?> GetUser(long telegramUserId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _users.TryGetValue(telegramUserId, out var user);
            return Task.FromResult(user);
        }
    }
}
