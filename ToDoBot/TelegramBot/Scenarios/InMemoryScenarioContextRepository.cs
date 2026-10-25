namespace TelegramBot.Scenarios;

public sealed class InMemoryScenarioContextRepository : IScenarioContextRepository
{
    private readonly Dictionary<long, ScenarioContext> _contexts = new();
    private readonly object _sync = new();

    public Task<ScenarioContext?> GetContext(long userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _contexts.TryGetValue(userId, out var context);
            return Task.FromResult(context);
        }
    }

    public Task SetContext(long userId, ScenarioContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        if (context.UserId != userId)
            throw new ArgumentException("Контекст принадлежит другому пользователю.", nameof(context));
        lock (_sync) _contexts[userId] = context;
        return Task.CompletedTask;
    }

    public Task ResetContext(long userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) _contexts.Remove(userId);
        return Task.CompletedTask;
    }
}
