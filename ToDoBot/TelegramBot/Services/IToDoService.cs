using TelegramBot.Models;

namespace TelegramBot.Services;

public interface IToDoService
{
    Task<ToDoItem> Add(ToDoUser user, string name, DateTime deadline, CancellationToken ct);
    Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct);
    Task<bool> Complete(Guid userId, Guid taskId, CancellationToken ct);
    Task<bool> Delete(Guid userId, Guid taskId, CancellationToken ct);
}
