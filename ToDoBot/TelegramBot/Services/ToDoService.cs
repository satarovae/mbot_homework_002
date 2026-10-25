using TelegramBot.Models;

namespace TelegramBot.Services;

public sealed class ToDoService : IToDoService
{
    public const int MaxNameLength = 128;
    private readonly Dictionary<Guid, ToDoItem> _items = new();
    private readonly object _sync = new();

    public Task<ToDoItem> Add(ToDoUser user, string name, DateTime deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
            throw new ArgumentException($"Название должно содержать от 1 до {MaxNameLength} символов.", nameof(name));

        var item = new ToDoItem { User = user, Name = name.Trim(), Deadline = deadline.Date };
        lock (_sync) _items.Add(item.Id, item);
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            IReadOnlyList<ToDoItem> result = _items.Values.Where(x => x.User.UserId == userId)
                .OrderBy(x => x.CreatedAt).ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<bool> Complete(Guid userId, Guid taskId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_items.TryGetValue(taskId, out var item) || item.User.UserId != userId)
                return Task.FromResult(false);
            item.State = ToDoItemState.Completed;
            return Task.FromResult(true);
        }
    }

    public Task<bool> Delete(Guid userId, Guid taskId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_items.TryGetValue(taskId, out var item) || item.User.UserId != userId)
                return Task.FromResult(false);
            return Task.FromResult(_items.Remove(taskId));
        }
    }
}
